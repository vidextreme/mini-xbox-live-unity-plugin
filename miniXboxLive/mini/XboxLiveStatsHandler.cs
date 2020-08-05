using Microsoft.Xbox.Services;
using Microsoft.Xbox.Services.Statistics.Manager;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static XboxLiveUserManager;

public partial class XboxLiveStatsHandler<T> where T : struct,
          IComparable,
          IComparable<T>,
          IConvertible,
          IEquatable<T>,
          IFormattable
{
    XboxLiveStatType _type;
    XboxLiveUserHandler _user;
    string _statsID;
    public XboxLiveStatType Type => _type;
    public string ID => _statsID;
    public XboxLiveStatsHandler(XboxLiveUserHandler user, string statsID)
    {
        Type type = typeof(T);
        if (type == typeof(int)
            || type == typeof(long))
        {
            _type = XboxLiveStatType.Integer;
        }
        else if (type == typeof(float)
            || type == typeof(double))
        {
            _type = XboxLiveStatType.Decimal;
        }
        else if (type == typeof(TimeSpan))
        {
            _type = XboxLiveStatType.Timespan;
        }
        else if (type == typeof(string))
        {
            _type = XboxLiveStatType.String;
        }
        else throw new Exception(string.Format("{0} is not supported", type.ToString()));
        _user = user;
        _statsID = statsID;
    }
    public T Value
    {
        set
        {
            object v = value;
            switch(_type)
            {
                case XboxLiveStatType.Integer:
                    _user.SetStatisticIntegerData(_statsID, (long)v); break;
                case XboxLiveStatType.Decimal:
                    _user.SetStatisticNumberData(_statsID, (double)v); break;
                case XboxLiveStatType.Timespan:
                    _user.SetStatisticStringData(_statsID, (string)v); break;
            }
        }
        get
        {
            try
            {
                StatisticValue statValue = _user.GetStatistic(_statsID);
                switch (_type)
                {
                    case XboxLiveStatType.Integer:
                        return (T)(object)statValue.AsInteger;
                    case XboxLiveStatType.Decimal:
                        return (T)(object)statValue.AsNumber;
                    case XboxLiveStatType.String:
                        return (T)(object)statValue.AsString;
                    default:
                        throw new Exception("This is an invalid Stats handler.");
                }
            }
            catch (Exception e)
            {
                // GetStatistic will fail with an exception if if its the 
                // first time reading the stat for example. 
                XboxLiveUserManager.Log(XboxLiveLogType.Error, e.Message);
                throw new Exception("This is an invalid Stats handler.");
            }
        }
    }
    public void RequestFlushStatsService(bool isHighPriority = false)
    {
        _user.RequestFlushStatsService(isHighPriority);
    }
}

public enum XboxLiveStatType
{
    Integer,
    Decimal,
    Timespan,
    String
}