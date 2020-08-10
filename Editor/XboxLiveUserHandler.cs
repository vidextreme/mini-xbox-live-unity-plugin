// Copyright (c) John David Bonifacio Uy
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Xbox.Services;
using Microsoft.Xbox.Services.Social.Manager;
using Microsoft.Xbox.Services.Statistics.Manager;

using UnityEngine;
using Windows.Gaming.XboxLive.Storage;
using Microsoft.Xbox.Services.ConnectedStorage;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage.Streams;
#endif

using XboxSocialUserFilter = System.Tuple<Microsoft.Xbox.Services.Social.Manager.RelationshipFilter, Microsoft.Xbox.Services.Social.Manager.PresenceFilter>;


[Serializable]
public class XboxLiveUserHandler
{
    public XboxLiveUserHandler(string playerCode, XboxLiveUser user, XboxLiveContext context, XboxLiveUserManager manager)
    {
        User = user;
        PlayerCode = playerCode;
        Manager = manager;
        Context = context;
    }
    public XboxSocialUserGroup ProfileUserGroup;
    public Dictionary<XboxSocialUserFilter, XboxSocialUserGroup> SocialGroups = new Dictionary<XboxSocialUserFilter, XboxSocialUserGroup>();
    public XboxLiveUserManager Manager { private set; get; }
    public string PlayerCode { private set; get; }
    public XboxLiveUser User { private set; get; }
    public XboxLiveContext Context { private set; get; }
    public XboxSocialUser SocialUser { private set; get; }

    public GameSaveProvider GameSave { private set; get; }

    public Sprite GamerPic { private set; get; }
    public Action OnReload = null;
    bool _registerToStats = false;
    public void LoadProfile()
    {
        XboxLive.Instance.SocialManager.AddLocalUser(User, SocialManagerExtraDetailLevel.PreferredColorLevel | SocialManagerExtraDetailLevel.TitleHistoryLevel);
    }
    public void RegisterToStats()
    {
        if (_registerToStats) return;

        XboxLive.Instance.StatsManager.AddLocalUser(User);
        _registerToStats = true;
    }
    public void UnregisterFromStats()
    { 
        if(_registerToStats)
            XboxLive.Instance.StatsManager.RemoveLocalUser(User);
        _registerToStats = false;
    }
    public void LoadSocial(XboxLiveUserRelationshipFilter relationshipFilter, params PresenceFilter[] presenceFilters)
    {
        if (SocialUser == null) throw new Exception("XboxSocialUser is invalid. Did you forget to call LoadProfile() first?");

        //XboxLive.Instance.StatsManager.AddLocalUser(user);
        if (presenceFilters.Length == 0)
            presenceFilters = new PresenceFilter[] { PresenceFilter.All };

        Action<RelationshipFilter> func = (rf) =>
        {
            foreach (PresenceFilter pf in presenceFilters)
            {
                XboxSocialUserFilter filter = new XboxSocialUserFilter(rf, pf);
                SocialGroups[filter] = XboxLive.Instance.SocialManager.CreateSocialUserGroupFromFilters(User, pf, rf);
            }
        };

        if ((relationshipFilter & XboxLiveUserRelationshipFilter.Favorite) != 0)
        {
            func(RelationshipFilter.Favorite);
        }

        if ((relationshipFilter & XboxLiveUserRelationshipFilter.Friends) != 0)
        {
            func(RelationshipFilter.Friends);
        }
    }
    public void _LoadSocialUser(Action<XboxLiveUserHandler> onDone, MonoBehaviour mb)
    {
        XboxLiveUserManager.Log("Getting Social User..." + ProfileUserGroup);
        SocialUser = ProfileUserGroup.Users.Where(user => user.XboxUserId == User.XboxUserId).FirstOrDefault();
        if (SocialUser.UseAvatar)
        {
            onDone(this);
        }
        else
        {
            mb.StartCoroutine(XboxLiveUserManager.LoadImage(SocialUser.DisplayPicRaw + "&w=128",
                                                        (tex) =>
                                                        {
                                                            if (tex)
                                                            {
                                                                var r = new Rect(0, 0, tex.width, tex.height);
                                                                this.GamerPic = Sprite.Create(tex, r, Vector2.zero);
                                                            }
                                                            onDone(this);
                                                        }));
        }
        /*XboxLiveUserManager.LoadImageAsync(SocialUser.DisplayPicRaw + "&w=128",
                                                    (tex) =>
                                                    {
                                                        var r = new Rect(0, 0, tex.width, tex.height);
                                                        this.GamerPic = Sprite.Create(tex, r, Vector2.zero);
                                                        onDone(this);
                                                    }).GetAwaiter().GetResult();*/
    }


    public void SetStatisticIntegerData(string statName, long value)
    {
        if (!_registerToStats) throw new Exception("User has not been registered to stats yet. Call RegisterToStats() first.");
        XboxLive.Instance.StatsManager.SetStatisticIntegerData(User, statName, value);
    }
    public void SetStatisticNumberData(string statName, double value)
    {
        if (!_registerToStats) throw new Exception("User has not been registered to stats yet. Call RegisterToStats() first.");
        XboxLive.Instance.StatsManager.SetStatisticNumberData(User, statName, value);
    }
    public void SetStatisticStringData(string statName, string value)
    {
        if (!_registerToStats) throw new Exception("User has not been registered to stats yet. Call RegisterToStats() first.");
        XboxLive.Instance.StatsManager.SetStatisticStringData(User, statName, value);
    }
    public StatisticValue GetStatistic(string statName)
    {
        if (!_registerToStats) throw new Exception("User has not been registered to stats yet. Call RegisterToStats() first.");
        return XboxLive.Instance.StatsManager.GetStatistic(User, statName);
    }
    public void RequestFlushStatsService(bool isHighPriority = false)
    {
        XboxLive.Instance.StatsManager.RequestFlushToService(User, isHighPriority);
    }

    public void InitializeGameSaveProvider()
    {
        Manager.InitializeGameSaveProvider((p, e) =>
        {
            switch(e)
            {
                case GameSaveStatus.Ok:
                    GameSave = p; break;
            }

            XboxLiveUserManager.Log(XboxLiveLogType.Normal, "Initialize Game Save Provider: " + e);
        }, PlayerCode);
    }

    public T CreateGameSaveContainer<T>() where T : XboxLiveGameSaveContainer<T>
    {
        if(GameSave == null)
        {
            throw new Exception("Please initialize the Game Save Provider first.");
        }

        return (T)Activator.CreateInstance(typeof(T), this);
    }
}
