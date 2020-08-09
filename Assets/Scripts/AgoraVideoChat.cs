using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using agora_gaming_rtc;
using static agora_gaming_rtc.ExternalVideoFrame;


/* NOTE: 
 *
 * This script handles the Agora-related functionality:
 * - Joining / Leaving Channels
 * - Creating / Deleting VideoSurface objects that enable us to see the camera feed of Agora party chat
 * - Managing the UI that contains the VideoSurface objects 
 *
 */



public class AgoraVideoChat : Photon.MonoBehaviour
{
    [Header("Agora Properties")]
    [SerializeField]
    private string appID = "APP_ID";
    [SerializeField]
    private string channel = "unity3d";
    private string originalChannel;
    private IRtcEngine mRtcEngine;
    private uint myUID = 0;

    [Header("Player Video Panel Properties")]
    [SerializeField]
    private GameObject userVideoPrefab;
    [SerializeField]
    private GameObject avatarVideoPrefab;
    [SerializeField]
    private Transform spawnPoint;
    [SerializeField]
    private RectTransform content;
    [SerializeField]
    private float spaceBetweenUserVideos = 150f;
    private List<GameObject> playerVideoList;

    public delegate void AgoraCustomEvent();
    public static event AgoraCustomEvent PlayerChatIsEmpty;
    public static event AgoraCustomEvent PlayerChatIsPopulated;

    public const TextureFormat ConvertFormat = TextureFormat.BGRA32;
    public const VIDEO_PIXEL_FORMAT PixelFormat = VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_BGRA;// note: RGBA is available from 3.0.1 and on


    Camera ARCamera;
    bool UseBanuba = true;

    void Start()
    {
        if (!photonView.isMine)
        {
            return;
        }

        playerVideoList = new List<GameObject>();

        // Setup Agora Engine and Callbacks.
        if (mRtcEngine != null)
        {
            IRtcEngine.Destroy();
        }

        originalChannel = channel;

        // -- These are all necessary steps to initialize the Agora engine -- //
        // Initialize Agora engine
        mRtcEngine = IRtcEngine.GetEngine(appID);

        // Setup our callbacks (there are many other Agora callbacks, however these are the calls we need).
        mRtcEngine.OnJoinChannelSuccess = OnJoinChannelSuccessHandler;
        mRtcEngine.OnUserJoined = OnUserJoinedHandler;
        mRtcEngine.OnLeaveChannel = OnLeaveChannelHandler;
        mRtcEngine.OnUserOffline = OnUserOfflineHandler;

        SetupVideoEngine();

        GameObject camObj = GameObject.Find("ARCamera");
        ARCamera = camObj.GetComponent<Camera>();

        // By setting our UID to "0" the Agora Engine creates a new one and assigns it. 
        mRtcEngine.JoinChannel(channel, null, 0);
    }

    public string GetCurrentChannel() => channel;

    public void JoinRemoteChannel(string remoteChannelName)
    {
        if (!photonView.isMine)
        {
            return;
        }

        mRtcEngine.LeaveChannel();

        mRtcEngine.JoinChannel(remoteChannelName, null, myUID);
        SetupVideoEngine();

        channel = remoteChannelName;
    }

    void SetupVideoEngine()
    {
        // Your video feed will not render if EnableVideo() isn't called. 
        mRtcEngine.EnableVideo();
        mRtcEngine.EnableVideoObserver();

        if (UseBanuba)
        {
            mRtcEngine.SetExternalVideoSource(true, false);
        }
    }
    /// <summary>
    /// Resets player Agora video chat party, and joins their original channel.
    /// </summary>
    public void JoinOriginalChannel()
    {
        if (!photonView.isMine)
        {
            return;
        }


        /* NOTE:
         * Say I'm in my original channel - "myChannel" - and someone joins me.
         * If I want to leave the party, and go back to my original channel, someone is already in it!
         * Therefore, if someone is inside "myChannel" and I want to be alone, I have to join a new channel that has the name of my unique Agora UID "304598093" (for example).
         */
        if (channel != originalChannel || channel == myUID.ToString())
        {
            channel = originalChannel;
        }
        else if (channel == originalChannel)
        {
            channel = myUID.ToString();
        }

        JoinRemoteChannel(channel);
    }

    #region Agora Callbacks
    // Local Client Joins Channel.
    private void OnJoinChannelSuccessHandler(string channelName, uint uid, int elapsed)
    {
        if (!photonView.isMine)
            return;

        myUID = uid;

        _isRunning = true;
        StartCoroutine(CoShareRenderData());
        CreateUserVideoSurface(uid, true, UseBanuba);
    }

    // Remote Client Joins Channel.
    private void OnUserJoinedHandler(uint uid, int elapsed)
    {
        if (!photonView.isMine)
            return;

        CreateUserVideoSurface(uid, false, false);
    }

    // Local user leaves channel.
    private void OnLeaveChannelHandler(RtcStats stats)
    {
        if (!photonView.isMine)
            return;

        foreach (GameObject player in playerVideoList)
        {
            Destroy(player.gameObject);
        }
        playerVideoList.Clear();
        _isRunning = false;
    }

    // Remote User Leaves the Channel.
    private void OnUserOfflineHandler(uint uid, USER_OFFLINE_REASON reason)
    {
        if (!photonView.isMine)
            return;

        if (playerVideoList.Count <= 1)
        {
            PlayerChatIsEmpty();
        }

        RemoveUserVideoSurface(uid);
    }
    #endregion

    // Create new image plane to display users in party.
    private void CreateUserVideoSurface(uint uid, bool isLocalUser, bool isAvatar)
    {
        // Avoid duplicating Local player VideoSurface image plane.
        for (int i = 0; i < playerVideoList.Count; i++)
        {
            if (playerVideoList[i].name == uid.ToString())
            {
                return;
            }
        }

        // Get the next position for newly created VideoSurface to place inside UI Container.
        float spawnY = playerVideoList.Count * spaceBetweenUserVideos;
        Vector3 spawnPosition = new Vector3(0, -spawnY, 0);

        // Create Gameobject that will serve as our VideoSurface.
        GameObject newUserVideo = Instantiate(isAvatar ? avatarVideoPrefab : userVideoPrefab, spawnPosition, spawnPoint.rotation);
        if (newUserVideo == null)
        {
            Debug.LogError("CreateUserVideoSurface() - newUserVideoIsNull");
            return;
        }
        newUserVideo.name = uid.ToString();
        newUserVideo.transform.SetParent(spawnPoint, false);

        playerVideoList.Add(newUserVideo);

        if (isAvatar)
        {
            // The AvatarViewController will encapsulate logic to assign user id
        }
        else
        {
            newUserVideo.transform.rotation = Quaternion.Euler(Vector3.right * -180);
            AssignAgoraSurface(newUserVideo, isLocalUser ? 0 : uid);
        }


        // Update our "Content" container that holds all the newUserVideo image planes
        content.sizeDelta = new Vector2(0, playerVideoList.Count * spaceBetweenUserVideos + 140);

        UpdatePlayerVideoPostions();
        UpdateLeavePartyButtonState();
    }

    void AssignAgoraSurface(GameObject newUserVideo, uint uid)
    {
        // Update our VideoSurface to reflect new users
        VideoSurface newVideoSurface = newUserVideo.GetComponent<VideoSurface>();
        if (newVideoSurface == null)
        {
            Debug.LogWarning("CreateUserVideoSurface() - VideoSurface component is null on newly joined user");

            newVideoSurface = newUserVideo.AddComponent<VideoSurface>();
        }

        newVideoSurface.SetForUser(uid);
        newVideoSurface.SetGameFps(30);
    }

    private void RemoveUserVideoSurface(uint deletedUID)
    {
        foreach (GameObject player in playerVideoList)
        {
            if (player.name == deletedUID.ToString())
            {
                playerVideoList.Remove(player);
                Destroy(player.gameObject);
                break;
            }
        }

        // update positions of new players
        UpdatePlayerVideoPostions();

        Vector2 oldContent = content.sizeDelta;
        content.sizeDelta = oldContent + Vector2.down * spaceBetweenUserVideos;
        content.anchoredPosition = Vector2.zero;

        UpdateLeavePartyButtonState();
    }

    private void UpdatePlayerVideoPostions()
    {
        for (int i = 0; i < playerVideoList.Count; i++)
        {
            playerVideoList[i].GetComponent<RectTransform>().anchoredPosition = Vector2.down * spaceBetweenUserVideos * i;
        }
    }

    private void UpdateLeavePartyButtonState()
    {
        if (playerVideoList.Count > 1)
        {
            PlayerChatIsPopulated();
        }
        else
        {
            PlayerChatIsEmpty();
        }
    }

    private void TerminateAgoraEngine()
    {
        if (mRtcEngine != null)
        {
            mRtcEngine.LeaveChannel();
            mRtcEngine = null;
            IRtcEngine.Destroy();
        }
    }

    private IEnumerator OnLeftRoom()
    {
        //Wait untill Photon is properly disconnected (empty room, and connected back to main server)
        while (PhotonNetwork.room != null || PhotonNetwork.connected == false)
            yield return 0;

        TerminateAgoraEngine();
    }

    // Cleaning up the Agora engine during OnApplicationQuit() is an essential part of the Agora process with Unity. 
    private void OnApplicationQuit()
    {
        TerminateAgoraEngine();
    }

    #region =========== Push Video =================
    Texture2D BufferTexture;
    bool _isRunning;
    IEnumerator CoShareRenderData()
    {
        if (ARCamera == null)
        {
            Debug.LogWarning("AR Camera is not present!");
            yield break;
        }
        while (_isRunning)
        {
            yield return new WaitForEndOfFrame();
            ShareRenderTexture();
        }
        Debug.LogWarning("CoShareRenderData ended.");
        yield return null;
    }


    /// <summary>
    ///   Get the image from renderTexture.  (AR Camera must assign a RenderTexture prefab in
    /// its renderTexture field.)
    /// </summary>
    private void ShareRenderTexture()
    {
        RenderTexture.active = ARCamera.targetTexture; // the targetTexture holds render texture
        Rect rect = new Rect(0, 0, ARCamera.targetTexture.width, ARCamera.targetTexture.height);
        BufferTexture = new Texture2D(ARCamera.activeTexture.width, ARCamera.activeTexture.height, ConvertFormat, false);
        BufferTexture.ReadPixels(rect, 0, 0);
        BufferTexture.Apply();

        byte[] bytes = BufferTexture.GetRawTextureData();

        // sends the Raw data contained in bytes
        StartCoroutine(PushFrame(bytes, (int)rect.width, (int)rect.height,
         () =>
         {
             bytes = null;
             Destroy(BufferTexture);
         }));
        RenderTexture.active = null;
    }

    int frameCnt = 0; // monotonic timestamp counter
    /// <summary>
    /// Push frame to the remote client.  This is the same code that does ScreenSharing.
    /// </summary>
    /// <param name="bytes">raw video image data</param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="onFinish">callback upon finish of the function</param>
    /// <returns></returns>
    IEnumerator PushFrame(byte[] bytes, int width, int height, System.Action onFinish)
    {
        if (bytes == null || bytes.Length == 0)
        {
            Debug.LogError("Zero bytes found!!!!");
            yield break;
        }

        IRtcEngine rtc = IRtcEngine.QueryEngine();
        //if the engine is present
        if (rtc != null)
        {
            //Create a new external video frame
            ExternalVideoFrame externalVideoFrame = new ExternalVideoFrame();
            //Set the buffer type of the video frame
            externalVideoFrame.type = ExternalVideoFrame.VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA;
            // Set the video pixel format
            externalVideoFrame.format = PixelFormat;
            //apply raw data you are pulling from the rectangle you created earlier to the video frame
            externalVideoFrame.buffer = bytes;
            //Set the width of the video frame (in pixels)
            externalVideoFrame.stride = width;
            //Set the height of the video frame
            externalVideoFrame.height = height;
            //Remove pixels from the sides of the frame
            externalVideoFrame.cropLeft = 10;
            externalVideoFrame.cropTop = 10;
            externalVideoFrame.cropRight = 10;
            externalVideoFrame.cropBottom = 10;
            //Rotate the video frame (0, 90, 180, or 270)
            //externalVideoFrame.rotation = 90;
            externalVideoFrame.rotation = 180;
            // increment i with the video timestamp
            externalVideoFrame.timestamp = frameCnt++;
            //Push the external video frame with the frame we just created
            int a =
            rtc.PushVideoFrame(externalVideoFrame);
            if (frameCnt % 500 == 0) Debug.Log(" pushVideoFrame(" + frameCnt + ") size:" + bytes.Length + " => " + a);

        }
        if (onFinish != null)
        {
            onFinish();
        }
        yield return null;
    }

    #endregion
}