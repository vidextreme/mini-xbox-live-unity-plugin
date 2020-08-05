using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Reflection;

using Microsoft.Xbox.Services;
using Microsoft.Xbox.Services.Client;
using Microsoft.Xbox.Services.Social.Manager;
using Microsoft.Xbox.Services.Statistics.Manager;
using Microsoft.Xbox.Services.Leaderboard;
using Microsoft.Xbox.Services.ConnectedStorage;
using Windows.Gaming.XboxLive.Storage;


using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using static XboxLiveUserManager;

#if ENABLE_WINMD_SUPPORT
using Windows.System;
using Windows.Storage.Streams;
#endif

public partial class XboxLiveUserManager : MonoBehaviour
{    
    ISocialManager _socialManager;
    IStatisticManager _statsManager;
    Dictionary<string, XboxLiveUserHandler> _users = new Dictionary<string, XboxLiveUserHandler>();
    Dictionary<XboxLiveUser, string> _usersReverseMap = new Dictionary<XboxLiveUser, string>();
    Dictionary<XboxSocialUserGroup, XboxLiveLeaderboardData> _pendingLeaderboards = new Dictionary<XboxSocialUserGroup, XboxLiveLeaderboardData>();

    public static XboxLiveUserManager Instance { protected set; get; }//for singleton access

    public XboxLiveLogEvent OnLog;

    [Header("Xbox Live Events")]
    public XboxLiveUserHandlerEvent OnSignIn;
    public XboxLiveUserPlayerCodeEvent OnSignOut;
    public XboxLiveUserHandlerEvent OnProfileLoaded;
    public XboxLiveStatisticEvent OnStatisticEvent;
    public XboxLiveLeaderboardEvent OnLeaderboardResult;
    public XboxLiveGameSaveStatusChange OnGameSaveLoaded;

    public static void Log(object msg)
    {
        Log(XboxLiveLogType.Normal, msg);
    }
    public static void Log(XboxLiveLogType type, object msg)
    {
        switch(type)
        {
            case XboxLiveLogType.Normal:
                Debug.Log(msg); break;
            case XboxLiveLogType.Warning:
                Debug.LogWarning(msg); break;
            case XboxLiveLogType.Error:
                Debug.LogError(msg); break;
        }

        Instance.OnLog.Invoke(type, msg);
    }
    private void Awake()
    {
        Instance = this;
        _socialManager = XboxLive.Instance.SocialManager;
        _statsManager = XboxLive.Instance.StatsManager;
    }
    private void OnDestroy()
    {
        Instance = null;
        //TODO cleanup
    }
    // Start is called before the first frame update
    void Start()
    {
        XboxLiveUser.SignOutCompleted += _OnSignOut;
    }

    // Update is called once per frame
    void Update()
    {
        try
        {
            var socialEvents = _socialManager.DoWork();

            foreach (SocialEvent socialEvent in socialEvents)
            {
                //if (XboxLiveServicesSettings.Instance.DebugLogsOn)
                //{
                //    Debug.LogFormat("[SocialManager] Processed {0} event.", socialEvent.EventType);
                //}
                OnSocialEvent(socialEvent);
            }

            var statsEvents = _statsManager.DoWork();
            foreach (StatisticEvent statsEvent in statsEvents)
            {
                //if (XboxLiveServicesSettings.Instance.DebugLogsOn)
                //{
                //    Debug.LogFormat("[SocialManager] Processed {0} event.", socialEvent.EventType);
                //}
                OnStatsEvent(statsEvent);
            }
        }
        catch (Exception ex)
        {
            Log(XboxLiveLogType.Error, ex.Message);
        }
    }
    public XboxLiveUserHandler GetUser(string playerCode)
    {
        XboxLiveUserHandler handler = null;
        _users.TryGetValue(playerCode, out handler);
        return handler;
    }
    void OnSocialEvent(SocialEvent socialEvent)
    {
        try
        {
            Log("On social event " + socialEvent.EventType);
            if (!_usersReverseMap.ContainsKey(socialEvent.User))
            {                
                return;
            }

            string playerCode = _usersReverseMap[socialEvent.User];
            XboxLiveUserHandler handler = _users[playerCode];
            //TODO error handling

            if (socialEvent.EventType == SocialEventType.LocalUserAdded)
            {
                handler.ProfileUserGroup = XboxLive.Instance.SocialManager.CreateSocialUserGroupFromList(handler.User, new List<string> { handler.User.XboxUserId });
                //handler._LoadSocialUser((h) => OnProfileLoaded.Invoke(h), this);
                //OnProfileLoaded.Invoke(handler);
                //LoadImageAsync(handler.User.DisplayPicRaw + "&w=128")
            }
            else if (socialEvent.EventType == SocialEventType.SocialUserGroupLoaded)
            {
                SocialUserGroupLoadedEventArgs args = (SocialUserGroupLoadedEventArgs)socialEvent.EventArgs;
                var sug = args.SocialUserGroup;
                if (sug == handler.ProfileUserGroup)
                {
                    handler._LoadSocialUser((h) => OnProfileLoaded.Invoke(h), this);
                }
                else if(_pendingLeaderboards.ContainsKey(sug))
                {
                    var data = _pendingLeaderboards[sug];
                    var players = data.Players;
                    int done = 0;
                    int nb = players.Length;
                    for (int i=0; i < nb; i++)
                    {
                        var player = players[i];
                        XboxSocialUser user = sug.Users.FirstOrDefault(x => x.XboxUserId == player.XboxUserId);
                        StartCoroutine(LoadImage(user.DisplayPicRaw + "&w=128", (tex) =>
                        {
                            if(tex)
                            {
                                var r = new Rect(0, 0, tex.width, tex.height);
                                player.Icon = Sprite.Create(tex, r, Vector2.zero);
                            }
                            done++;
                            if(done >= nb)
                            {
                                _pendingLeaderboards.Remove(sug);
                                OnLeaderboardResult.Invoke(handler, data);
                            }
                        }));
                    }
                }
            }
            else
            {
                foreach (XboxLiveUserHandler h in _users.Values)
                {

                }
            }
        }
        catch (Exception e)
        {
            Log(XboxLiveLogType.Error, e.Message);
        }
    }
    void OnStatsEvent(StatisticEvent statsEvent)
    {
        try
        {
            string playerCode = _usersReverseMap[statsEvent.User];
            XboxLiveUserHandler handler = _users[playerCode];

            OnStatisticEvent.Invoke(handler, statsEvent);

            switch(statsEvent.EventType)
            {
                case StatisticEventType.LocalUserAdded:
                    Debug.Log(string.Format("{0}: XboxID:{1} registered to statistic manager.", handler.PlayerCode, handler.User.XboxUserId)); break;
                case StatisticEventType.GetLeaderboardComplete:
                    LeaderboardResultEventArgs result = (LeaderboardResultEventArgs)statsEvent.EventArgs;
                    List<string> userIds = new List<string>();
                    XboxLiveLeaderBoardEntry[] entries = result.Result.Rows.Select((x) =>
                    {
                        XboxLiveLeaderBoardEntry entry = new XboxLiveLeaderBoardEntry();
                        entry.Rank = x.Rank;
                        entry.XboxUserId = x.XboxUserId;
                        userIds.Add(x.XboxUserId);
                        entry.GamerTag = x.Gamertag;
                        entry.OtherValues = x.Values.ToArray();
                        return entry;
                    }).ToArray();
                    XboxLiveLeaderboardData data = new XboxLiveLeaderboardData(handler, result.Result, entries);
                    XboxSocialUserGroup socialGroup = XboxLive.Instance.SocialManager.CreateSocialUserGroupFromList(handler.User, userIds);
                    _pendingLeaderboards[socialGroup] = data;
                    break;
            }

        }
        catch (Exception e)
        {
            Log(XboxLiveLogType.Error, e.Message);
        }
    }
    public bool IsSignedIn(string playerCode = "player_one")
    {
        return _users.ContainsKey(playerCode);
    }
    public async void SignIn(string playerCode = "player_one")
    {
        if (_users.ContainsKey(playerCode))
            throw new Exception(playerCode + " has already signing/signed in.");
        _users[playerCode] = null;
        //StartCoroutine(PickUser(playerCode, _OnPickedUser));
        await PickUserEx(playerCode, _OnPickedUser);
    }
    public void SignOut(string playerCode = "player_one")
    {
        XboxLiveUserHandler handler = GetUser(playerCode);
        if (handler != null)
        {
            handler.UnregisterFromStats();
            XboxLive.Instance.SocialManager.RemoveLocalUser(handler.User);
            _usersReverseMap.Remove(handler.User);
            _users.Remove(playerCode);
            OnSignOut.Invoke(playerCode);
        }
    }
    public void InitializeGameSaveProvider(Action<GameSaveProvider, GameSaveStatus> ondone, string playerCode = "player_one")
    {
        XboxLiveUserHandler handler = GetUser(playerCode);
        if (handler != null)
        {
            ondone += (p, e) =>
            {
                OnGameSaveLoaded.Invoke(handler, e);
            };
            StartCoroutine(_InitializeGameSaveProvider(handler, ondone));
        }
    }
    public void QueryLeaderboard(string statsID, string playerCode = "player_one", bool skipToPlayerRank = true, SortOrder order = SortOrder.Descending, uint maxItemsPerQuery = 10, uint skipResultToRank = 0)
    {
        XboxLiveUserHandler handler;
        if (_users.TryGetValue(playerCode, out handler))
        {
            LeaderboardQuery query = new LeaderboardQuery();
            if(skipResultToRank == 0)
            {
                query.SkipResultToMe = skipToPlayerRank;
            }
            else
            {
                query.SkipResultToRank = skipResultToRank;
            }

            query.Order = order;
            query.MaxItems = maxItemsPerQuery;

            XboxLive.Instance.StatsManager.GetLeaderboard(handler.User, statsID, query);
        }
    }
    void _OnPickedUser(string playerCode, XboxLiveUser user, XboxLiveContext context)
    {
        var handler = _users[playerCode] = new XboxLiveUserHandler(playerCode, user, context, this);
        _usersReverseMap[user] = playerCode;
        OnSignIn.Invoke(handler);
    }

    async Task PickUserEx(string playerCode, Action<string, XboxLiveUser, XboxLiveContext> onUserSignIn)
    {
        bool signedIn = false;

        XboxLiveUser user = null;

        try
        {
#if ENABLE_WINMD_SUPPORT
            // Get a list of the active Windows users.
            IReadOnlyList<Windows.System.User> users = await Windows.System.User.FindAllAsync();
            Windows.System.User windowsUser = null;
            if (users.Count == 0)
            {
                var autoPicker = new Windows.System.UserPicker { AllowGuestAccounts = false };
                windowsUser = await autoPicker.PickSingleUserAsync().AsTask();/*.ContinueWith(
                        task =>
                        {
                            if (task.Exception != null)
                                Debug.LogError("Exception occured: " + task.Exception.Message);
                            else if (task.Status == TaskStatus.RanToCompletion)
                            {
                                WindowsSystemUser = task.Result;
                                done = true;
                            }
                        });*/
                //while (!done) yield return null;
                //user = usersTask.r
                if(windowsUser == null)
                {
                    Log(XboxLiveLogType.Warning, "Windows User NOT Found!");
                }
            }
            else
            {
                windowsUser = users[0];
            }
            user = new XboxLiveUser(windowsUser);
#else
            user = new XboxLiveUser();
#endif
            SignInResult signInSilentResult = await user.SignInSilentlyAsync();
            switch (signInSilentResult.Status)
            {
                case SignInStatus.Success:
                    signedIn = true;
                    break;
                case SignInStatus.UserInteractionRequired:
                    //3. Attempt to sign-in with UX if required
                    SignInResult signInLoud = await user.SignInAsync();
                    switch (signInLoud.Status)
                    {
                        case SignInStatus.Success:
                            signedIn = true;
                            break;
                        case SignInStatus.UserCancel:
                            // present in-game UX that allows the user to retry the sign-in operation. (For example, a sign-in button)
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
        }
        catch (Exception e)
        {
            Log(XboxLiveLogType.Error, e.Message);
        }

        if (user!=null)
        {
            // 4. Create an Xbox Live context based on the interacting user
            XboxLiveContext context = new Microsoft.Xbox.Services.XboxLiveContext(user);

            onUserSignIn(playerCode, user, context);
        }
        else
        {
            Log(XboxLiveLogType.Warning, "Windows User NOT Found!");
        }
    }
    void _OnSignOut(object sender, SignOutCompletedEventArgs e)
    {
        XboxLiveUser user = e.User;
        string playerCode = _usersReverseMap[user];
        SignOut(playerCode);
    }
    IEnumerator PickUser(string playerCode, Action<string, XboxLiveUser, XboxLiveContext> onUserSignIn)
    {
        XboxLiveUser user;
#if ENABLE_WINMD_SUPPORT
        var autoPicker = new Windows.System.UserPicker { AllowGuestAccounts = false };
        var done = false;
        Windows.System.User WindowsSystemUser = null;
        var usersTask = autoPicker.PickSingleUserAsync().AsTask().ContinueWith(
                        task =>
                        {
                            if (task.Exception != null)
                                Debug.LogError("Exception occured: " + task.Exception.Message);
                            else if (task.Status == TaskStatus.RanToCompletion)
                            {
                                WindowsSystemUser = task.Result;
                                done = true;
                            }
                        });
        while (!done) yield return null;

        user = new XboxLiveUser(WindowsSystemUser);
#else
        user = new XboxLiveUser();
#endif
        if (user != null)
        {
            Log("Windows User Found!");
            XboxLiveContext context = new Microsoft.Xbox.Services.XboxLiveContext(user);
            StartCoroutine(_SignIn(playerCode, user, context, onUserSignIn));
        }
        else
        {
            Log(XboxLiveLogType.Warning, "Windows User NOT Found!");
        }
        yield return null;
    }
    IEnumerator _SignIn(string playerCode, XboxLiveUser user, XboxLiveContext context, Action<string, XboxLiveUser, XboxLiveContext> onUserSignIn)
    {
        Log("Signing in (silent)...");
        SignInStatus signInStatus = SignInStatus.Success;
        TaskYieldInstruction<SignInResult> signInSilentlyTask = user.SignInSilentlyAsync().AsCoroutine();
        yield return signInSilentlyTask;

        try
        {
            signInStatus = signInSilentlyTask.Result.Status;
        }
        catch (Exception ex)
        {
            Log(XboxLiveLogType.Error, ex.Message);
        }

        if (signInStatus != SignInStatus.Success)
        {
            Log("Signing in again...");
            TaskYieldInstruction<SignInResult> signInTask = user.SignInAsync().AsCoroutine();
            yield return signInTask;

            try
            {
                signInStatus = signInTask.Result.Status;
            }
            catch (Exception ex)
            {
                Log(XboxLiveLogType.Error, ex.Message);
            }
        }
        if (signInStatus == SignInStatus.Success)
        {
            Log("Signing callback...");
            onUserSignIn(playerCode, user, context);
        }

        //TODO failed feedback
    }
    
    IEnumerator _InitializeGameSaveProvider(XboxLiveUserHandler handler, Action<GameSaveProvider, GameSaveStatus> ondone)
    {
        yield return null;

        try
        {
#if ENABLE_WINMD_SUPPORT
            var configId = XboxLive.Instance.AppConfig.ServiceConfigurationId;
            Windows.System.User windowsUser = handler.User.WindowsSystemUser;
            var initTask = GameSaveProvider.GetForUserAsync(windowsUser, configId).AsTask();
            GameSaveProvider v = initTask.Result.Value;
            if (initTask.Result.Status == GameSaveErrorStatus.Ok)
            {
                ondone(v, GameSaveStatus.Ok);
            }
            else
            {            
                var gameSaveStatus = (GameSaveStatus)initTask.Result.Status;
                    //(GameSaveStatus)Enum.Parse(typeof(GameSaveStatus), initTask.Result.Status.ToString());
                ondone(v, gameSaveStatus);
            }
#else
            ondone(new GameSaveProvider(), GameSaveStatus.Ok);
#endif
        }
        catch(Exception e)
        {
            Log(XboxLiveLogType.Error, "Initialize Game Save Provider: " + e.Message);
        }
    }

    public static IEnumerator LoadImage(string url, Action<Texture2D> ondone)
    {
        UnityWebRequest uwr = null;
        try
        {
            uwr = UnityWebRequestTexture.GetTexture(url);
        }
        catch(Exception e)
        {
            Log(XboxLiveLogType.Error, e.Message);
        }
        
        yield return uwr.SendWebRequest();

        Texture2D texture = null;
        try
        {
            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Log(XboxLiveLogType.Error, uwr.error);
            }
            else
            {
                // Get downloaded asset bundle
                texture = DownloadHandlerTexture.GetContent(uwr);
            }
        }
        catch (Exception e)
        {
            Log(XboxLiveLogType.Error, e.Message);
        }
        finally
        {
            uwr.Dispose();
            ondone(texture);
        }
    }

    public static async Task LoadImageAsync(string url, Action<Texture2D> ondone)
    {
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            await uwr.SendWebRequest();
            Texture2D texture = null;
            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Log(XboxLiveLogType.Error, uwr.error);
            }
            else
            {
                // Get downloaded asset bundle
                texture = DownloadHandlerTexture.GetContent(uwr);
            }
            ondone(texture);
        }
    }

}

[Serializable]
public class XboxLiveLogEvent : UnityEvent<XboxLiveLogType,object>
{
}

[Serializable]
public class XboxLiveUserHandlerEvent : UnityEvent<XboxLiveUserHandler>
{
}
[Serializable]
public class XboxLiveUserPlayerCodeEvent : UnityEvent<string>
{
}
[Serializable]
public class XboxLiveStatisticEvent : UnityEvent<XboxLiveUserHandler, StatisticEvent>
{
}
[Serializable]
public class XboxLiveLeaderboardEvent : UnityEvent<XboxLiveUserHandler, XboxLiveLeaderboardData>
{
}

[Serializable]
public class XboxLiveGameSaveStatusChange : UnityEvent<XboxLiveUserHandler, GameSaveStatus>
{
}
public class XboxLiveLeaderboardData
{
    public readonly LeaderboardResult Result;
    public readonly XboxLiveUserHandler Handler;
    public readonly XboxLiveLeaderBoardEntry[] Players;
    public XboxLiveLeaderboardData(XboxLiveUserHandler handler, LeaderboardResult result, XboxLiveLeaderBoardEntry[] players)
    {
        Result = result;
        Handler = handler;
        Players = players;
    }
    public bool HasNext => Result.HasNext;

    public void QueryNext()
    {
        if(Result.HasNext)
        {
            LeaderboardQuery query = Result.GetNextQuery();
            XboxLive.Instance.StatsManager.GetLeaderboard(Handler.User, query.StatName, query);
        }
    }
}

public class XboxLiveLeaderBoardEntry
{
    public string XboxUserId;
    public string GamerTag;
    public uint Rank;
    public Sprite Icon;
    public string[] OtherValues;
}
public class UnityWebRequestAwaiter : INotifyCompletion
{
    UnityWebRequestAsyncOperation _asyncOp;
    Action _onDone;

    public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
    {
        this._asyncOp = asyncOp;
        asyncOp.completed += (operation) =>
        {
            _onDone();
        };
    }

    public bool IsCompleted => _asyncOp.isDone;
    public void GetResult() { }
    public void OnCompleted(Action continuation)
    {
        this._onDone = continuation;
    }
}
public static partial class XboxLiveEntensions
{
    public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
    {
        return new UnityWebRequestAwaiter(asyncOp);
    }
}


public enum XboxLiveLogType
{
    Normal,
    Error,
    Warning
}
public enum XboxLiveUserRelationshipFilter
{
    Friends = 1,
    Favorite = 1 << 1,
    All = Friends | Favorite
}

#if !ENABLE_WINMD_SUPPORT

namespace Windows.Gaming.XboxLive.Storage
{
    public class GameSaveProvider
    {

    }
    public class DataWriter
    {

    }
}

#endif
