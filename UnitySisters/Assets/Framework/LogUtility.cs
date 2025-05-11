
using System.Collections;
using System.Collections.Generic;

public static class LogUtility 
{
    public delegate string EnumeratorLogAction<T>(in T value);

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object msg)
    {
        UnityEngine.Debug.Log(msg);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object msg)
    {
        UnityEngine.Debug.LogWarning(msg);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object msg)
    {
        UnityEngine.Debug.LogError(msg);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log<T>(IList<T> list, EnumeratorLogAction<T> logFun)
    {
        int count = list.Count;

        for (int i = 0; i < count; i++)
        {
            UnityEngine.Debug.Log(logFun(list[i]));
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log<T>(IList<T> list)
    {
        int count = list.Count;

        for (int i = 0; i < count; i++)
        {
            UnityEngine.Debug.Log(list[i]);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log<T>(IEnumerable<T> enumerable)
    {
        var enumerator = enumerable.GetEnumerator();

        while (enumerator.MoveNext())
        {
            UnityEngine.Debug.Log(enumerator.Current);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log<T>(IEnumerable<T> enumerable, EnumeratorLogAction<T> logFun)
    {
        var enumerator = enumerable.GetEnumerator();

        while (enumerator.MoveNext())
        {
            UnityEngine.Debug.Log(logFun(enumerator.Current));
        }
    }

    private static void EnumeratorLog<T>(in T value)
    {
        UnityEngine.Debug.Log(value);
    }
}


