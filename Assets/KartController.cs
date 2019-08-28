using Cinemachine;
using DG.Tweening;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class KartController : MonoBehaviour, IPunObservable, IInRoomCallbacks
{
    private PostProcessVolume postVolume;
    private PostProcessProfile postProfile;

    public Transform kartModel;
    public Transform kartNormal;
    public Rigidbody sphere;

    public List<ParticleSystem> primaryParticles = new List<ParticleSystem>();
    public List<ParticleSystem> secondaryParticles = new List<ParticleSystem>();

    float speed, currentSpeed;
    float rotate, currentRotate;
    int driftDirection;
    float driftPower;
    int driftMode = 0;
    bool first, second, third;
    Color c;

    [Header("Bools")]
    public bool drifting;

    [Header("Parameters")]

    public float acceleration = 30f;
    public float steering = 80f;
    public float gravity = 10f;
    public LayerMask layerMask;

    [Header("Model Parts")]

    public Transform frontWheels;
    public Transform backWheels;
    public Transform steeringWheel;

    [Header("Particles")]
    public Transform wheelParticles;
    public Transform flashParticles;
    public Color[] turboColors;

    [Header("Networking")]
    public PhotonView photonView;
    public GameObject cameraParent;
    public TextMesh nameMesh;
    public TextMesh messageMesh;

    void Start()
    {
        postVolume = Camera.main.GetComponent<PostProcessVolume>();
        postProfile = postVolume.profile;

        for (int i = 0; i < wheelParticles.GetChild(0).childCount; i++)
        {
            primaryParticles.Add(wheelParticles.GetChild(0).GetChild(i).GetComponent<ParticleSystem>());
        }

        for (int i = 0; i < wheelParticles.GetChild(1).childCount; i++)
        {
            primaryParticles.Add(wheelParticles.GetChild(1).GetChild(i).GetComponent<ParticleSystem>());
        }

        foreach (ParticleSystem p in flashParticles.GetComponentsInChildren<ParticleSystem>())
        {
            secondaryParticles.Add(p);
        }

        var rigidBody = sphere.gameObject.GetComponent<Rigidbody>();
        rigidBody.isKinematic = !photonView.IsMine;

        cameraParent.SetActive(photonView.IsMine);

        nameMesh.text = photonView.Controller.NickName;
        transform.parent.name = photonView.Controller.NickName;

        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDestroy()
    {
        Destroy(transform.parent.gameObject);
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    void Update()
    {
        //Follow Collider
        transform.position = sphere.transform.position - new Vector3(0, 0.4f, 0);
        nameMesh.transform.forward = Camera.main.transform.forward;
        messageMesh.transform.forward = Camera.main.transform.forward;

        if (photonView.IsMine)
        {

            //Accelerate
            if (Input.GetButton("Fire1"))
                speed = acceleration;

            //Steer
            if (Input.GetAxis("Horizontal") != 0)
            {
                int dir = Input.GetAxis("Horizontal") > 0 ? 1 : -1;
                float amount = Mathf.Abs((Input.GetAxis("Horizontal")));
                Steer(dir, amount);
            }

            //Drift
            if (Input.GetButtonDown("Jump") && !drifting && Input.GetAxis("Horizontal") != 0)
            {
                drifting = true;
                driftDirection = Input.GetAxis("Horizontal") > 0 ? 1 : -1;

                foreach (ParticleSystem p in primaryParticles)
                {
                    p.startColor = Color.clear;
                    p.Play();
                }

                kartModel.parent.DOComplete();
                kartModel.parent.DOPunchPosition(transform.up * .2f, .3f, 5, 1);

            }

            if (drifting)
            {
                float control = (driftDirection == 1) ? ExtensionMethods.Remap(Input.GetAxis("Horizontal"), -1, 1, 0, 2) : ExtensionMethods.Remap(Input.GetAxis("Horizontal"), -1, 1, 2, 0);
                float powerControl = (driftDirection == 1) ? ExtensionMethods.Remap(Input.GetAxis("Horizontal"), -1, 1, .2f, 1) : ExtensionMethods.Remap(Input.GetAxis("Horizontal"), -1, 1, 1, .2f);
                Steer(driftDirection, control);
                driftPower += powerControl * 50f * Time.deltaTime;

                ColorDrift();
            }

            if (Input.GetButtonUp("Jump") && drifting)
            {
                Boost();
            }

            currentSpeed = Mathf.SmoothStep(currentSpeed, speed, Time.deltaTime * 12f); speed = 0f;
            currentRotate = Mathf.Lerp(currentRotate, rotate, Time.deltaTime * 4f); rotate = 0f;

            //Animations    

            //a) Kart
            if (!drifting)
            {
                kartModel.localEulerAngles = Vector3.Lerp(kartModel.localEulerAngles, new Vector3(0, 90 + (Input.GetAxis("Horizontal") * 15), kartModel.localEulerAngles.z), .2f);
            }
            else
            {
                float control = (driftDirection == 1) ? ExtensionMethods.Remap(Input.GetAxis("Horizontal"), -1, 1, .5f, 2) : ExtensionMethods.Remap(Input.GetAxis("Horizontal"), -1, 1, 2, .5f);
                kartModel.parent.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(kartModel.parent.localEulerAngles.y, (control * 15) * driftDirection, .2f), 0);
            }

            //b) Wheels
            frontWheels.localEulerAngles = new Vector3(0, (Input.GetAxis("Horizontal") * 15), frontWheels.localEulerAngles.z);
            frontWheels.localEulerAngles += new Vector3(0, 0, sphere.velocity.magnitude / 2);
            backWheels.localEulerAngles += new Vector3(0, 0, sphere.velocity.magnitude / 2);

            //c) Steering Wheel
            steeringWheel.localEulerAngles = new Vector3(-25, 90, ((Input.GetAxis("Horizontal") * 45)));

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (GUI.GetNameOfFocusedControl() == "txtMessage")
                {
                    GUI.FocusControl("btnMessage");
                }
                else
                {
                    GUI.FocusControl("txtMessage");
                }
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                SendMessage();
            }
        }
    }

    //$$
    private string message = null;

    private void OnGUI()
    {
        if (!photonView.IsMine)
        {
            return;
        }

        GUI.SetNextControlName("txtMessage");
        message = GUI.TextField(new Rect(0f, Screen.height - 16f, Screen.width - 100f, 16f), message);

        GUI.SetNextControlName("btnMessage");
        if (GUI.Button(new Rect(Screen.width - 100f, Screen.height - 16f, 100f, 16f), "Send message"))
        {
            SendMessage();
        }
    }

    private void SendMessage()
    {
        photonView.RPC(nameof(RPC_ChatMessage), RpcTarget.AllViaServer, photonView.Controller.NickName, message);
        message = null;
        GUI.FocusControl("txtMessage");
    }

    [PunRPC]
    public void RPC_ChatMessage(string playerName, string message)
    {
        StartCoroutine(ShowMessageAsync(playerName, message));
    }

    [PunRPC]
    public void RPC_PlayBoostAnimations()
    {
        kartModel.Find("Tube001").GetComponentInChildren<ParticleSystem>().Play();
        kartModel.Find("Tube002").GetComponentInChildren<ParticleSystem>().Play();
    }

    private IEnumerator ShowMessageAsync(string playerName, string message)
    {
        messageMesh.text = $"{playerName}: {message}";
        messageMesh.transform.localPosition = Vector3.up;
        messageMesh.transform.DOScale(Vector3.one, .25f);
        messageMesh.transform.DOPunchPosition(Vector3.up * .25f, .25f);

        yield return new WaitForSeconds(3f);

        messageMesh.transform.DOPunchPosition(-Vector3.up * .25f, .25f);
        messageMesh.transform.DOScale(Vector3.zero, .25f);
    }

    private void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            //Forward Acceleration
            if (!drifting)
                sphere.AddForce(-kartModel.transform.right * currentSpeed, ForceMode.Acceleration);
            else
                sphere.AddForce(transform.forward * currentSpeed, ForceMode.Acceleration);

            //Gravity
            sphere.AddForce(Vector3.down * gravity, ForceMode.Acceleration);

            //Steering
            transform.eulerAngles = Vector3.Lerp(transform.eulerAngles, new Vector3(0, transform.eulerAngles.y + currentRotate, 0), Time.fixedDeltaTime * 5f);

            RaycastHit hitOn;
            RaycastHit hitNear;

            Physics.Raycast(transform.position + (transform.up * .1f), Vector3.down, out hitOn, 1.1f, layerMask);
            Physics.Raycast(transform.position + (transform.up * .1f), Vector3.down, out hitNear, 2.0f, layerMask);

            //Normal Rotation
            kartNormal.up = Vector3.Lerp(kartNormal.up, hitNear.normal, Time.fixedDeltaTime * 8.0f);
            kartNormal.Rotate(0, transform.eulerAngles.y, 0);
        }
    }

    public void Boost()
    {
        drifting = false;

        if (driftMode > 0)
        {
            DOVirtual.Float(currentSpeed * 3, currentSpeed, .3f * driftMode, Speed);
            DOVirtual.Float(0, 1, .5f, ChromaticAmount).OnComplete(() => DOVirtual.Float(1, 0, .5f, ChromaticAmount));
            photonView.RPC(nameof(RPC_PlayBoostAnimations), RpcTarget.All);
        }

        driftPower = 0;
        driftMode = 0;
        first = false; second = false; third = false;

        foreach (ParticleSystem p in primaryParticles)
        {
            p.startColor = Color.clear;
            p.Stop();
        }

        kartModel.parent.DOLocalRotate(Vector3.zero, .5f).SetEase(Ease.OutBack);

    }

    public void Steer(int direction, float amount)
    {
        rotate = (steering * direction) * amount;
    }

    public void ColorDrift()
    {
        if (!first)
            c = Color.clear;

        if (driftPower > 50 && driftPower < 100 - 1 && !first)
        {
            first = true;
            c = turboColors[0];
            driftMode = 1;

            PlayFlashParticle(c);
        }

        if (driftPower > 100 && driftPower < 150 - 1 && !second)
        {
            second = true;
            c = turboColors[1];
            driftMode = 2;

            PlayFlashParticle(c);
        }

        if (driftPower > 150 && !third)
        {
            third = true;
            c = turboColors[2];
            driftMode = 3;

            PlayFlashParticle(c);
        }

        foreach (ParticleSystem p in primaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
        }

        foreach (ParticleSystem p in secondaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
        }
    }

    void PlayFlashParticle(Color c)
    {
        GameObject.Find("CM vcam1").GetComponent<CinemachineImpulseSource>().GenerateImpulse();

        foreach (ParticleSystem p in secondaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
            p.Play();
        }
    }

    private void Speed(float x)
    {
        currentSpeed = x;
    }

    void ChromaticAmount(float x)
    {
        postProfile.GetSetting<ChromaticAberration>().intensity.value = x;
    }

    void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(frontWheels.localEulerAngles.y);
            stream.SendNext(steeringWheel.localEulerAngles.z);
            stream.SendNext(driftPower);
            stream.SendNext(primaryParticles[0].isPlaying);
            stream.SendNext(secondaryParticles[0].isPlaying);
        }

        if (stream.IsReading)
        {
            frontWheels.localEulerAngles = new Vector3(0, (float)stream.ReceiveNext(), frontWheels.localEulerAngles.z);

            frontWheels.localEulerAngles += new Vector3(0, 0, sphere.velocity.magnitude / 2);
            backWheels.localEulerAngles += new Vector3(0, 0, sphere.velocity.magnitude / 2);

            steeringWheel.localEulerAngles = new Vector3(-25, 90, (float)stream.ReceiveNext());

            var oldDriftPower = driftPower;
            driftPower = (float)stream.ReceiveNext();
            if (driftPower != oldDriftPower && driftPower == 0f)
            {
                first = second = third = false;
            }

            var isPlayingPrimaryParticles = (bool)stream.ReceiveNext();
            if (primaryParticles.Count > 0)
            {
                if (isPlayingPrimaryParticles != primaryParticles[0].isPlaying)
                {
                    if (isPlayingPrimaryParticles)
                    {
                        foreach (var particleSystem in primaryParticles)
                        {
                            particleSystem.Play();
                        }
                    }
                    else
                    {
                        foreach (var particleSystem in primaryParticles)
                        {
                            particleSystem.Stop();
                        }
                    }
                }
            }

            var isPlayingSecondaryParticles = (bool)stream.ReceiveNext();
            if (secondaryParticles.Count > 0)
            {
                if (isPlayingSecondaryParticles != secondaryParticles[0].isPlaying)
                {
                    if (isPlayingSecondaryParticles)
                    {
                        foreach (var particleSystem in secondaryParticles)
                        {
                            particleSystem.Play();
                        }
                    }
                    else
                    {
                        foreach (var particleSystem in secondaryParticles)
                        {
                            particleSystem.Stop();
                        }
                    }
                }
            }

            ColorDrift();
        }
    }

    void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
    {
    }

    void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
    {
        if (photonView.Controller == otherPlayer)
        {
            Destroy(gameObject);
        }
    }

    void IInRoomCallbacks.OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
    }

    void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
    }

    void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
    {
    }

    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawLine(transform.position + transform.up, transform.position - (transform.up * 2));
    //}
}
