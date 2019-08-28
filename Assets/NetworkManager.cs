using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{

    private string roomName = "MixAndJam";
    private string playerName = "MixAndJammer";
    private RoomInfo[] rooms = null;
    private TypedLobby lobby = new TypedLobby("MixAndJam", LobbyType.Default);

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        base.OnRoomListUpdate(roomList);

        rooms = roomList.ToArray();
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        PhotonNetwork.Instantiate("Player", new Vector3(6.33f, 1.25f, 0.56f), Quaternion.identity);
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();

        PhotonNetwork.JoinLobby(lobby);
    }

    private void Start()
    {
        PhotonNetwork.SendRate *= 2;
        PhotonNetwork.SerializationRate *= 2;
    }

    private void OnGUI()
    {
        GUILayout.Label($"Networking status: {PhotonNetwork.NetworkClientState}");

        if (PhotonNetwork.IsConnectedAndReady)
        {
            if (PhotonNetwork.InLobby)
            {
                if (string.IsNullOrEmpty(PhotonNetwork.NickName))
                {
                    GUILayout.Label("Enter a username");
                    playerName = GUILayout.TextField(playerName);
                    if (GUILayout.Button("Ok"))
                    {
                        PhotonNetwork.NickName = playerName;
                    }
                }
                else
                {
                    if (PhotonNetwork.InRoom)
                    {
                        GUILayout.Label($"In room {PhotonNetwork.CurrentRoom.Name} with {PhotonNetwork.CurrentRoom.PlayerCount - 1} others");
                    }
                    else
                    {
                        GUILayout.Label("Create a room");
                        roomName = GUILayout.TextField(roomName);
                        if (GUILayout.Button("Create Room"))
                        {
                            var options = new RoomOptions();
                            options.CleanupCacheOnLeave = true;
                            PhotonNetwork.CreateRoom(roomName, options, lobby);
                        }

                        GUILayout.Space(10f);
                        if (GUILayout.Button("Join a random room"))
                        {
                            PhotonNetwork.JoinRandomRoom();
                        }

                        GUILayout.Space(10f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Join a room");

                        if (GUILayout.Button("Refresh"))
                        {
                            PhotonNetwork.GetCustomRoomList(lobby, string.Empty);
                        }

                        GUILayout.EndHorizontal();
                        if (rooms != null)
                        {
                            for (int i = 0; i < rooms.Length; i++)
                            {
                                if (GUILayout.Button($"{rooms[i].Name} | {rooms[i].PlayerCount} players"))
                                {
                                    PhotonNetwork.JoinRoom(rooms[i].Name);
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (GUILayout.Button("Connect"))
            {
                if (!PhotonNetwork.ConnectUsingSettings())
                {
                    Debug.LogError("Failed to connect to photon");
                }
            }
        }
    }

}
