using System.Collections.Generic;
using System;
using NLog;


#region Description
// 
// RestServiceProvider keeps track on available rest api methods
// Each method delegate must be like "string Process(Dictionnary<string,string> parameters,string RequestBody)";
// You can register new method using RegisterMethod
// You can then invoke registered method using InvokeMethod
// 
#endregion

namespace RestServiceProvider
{
    public class RequestDispatcher
    {
        private static Logger l = NLog.LogManager.GetCurrentClassLogger();

        private object Lock = new object();

        private readonly Dictionary<string, Func<Dictionary<string, string>, string, string>> MethodMap = new Dictionary<string, Func<Dictionary<string, string>, string, string>>();

        public void UnregisterAllMethods() => MethodMap.Clear();

        public bool IsMethodRegistered(string RelativeUrl) => MethodMap.ContainsKey(RelativeUrl);

        public string InvokeMethod(string RelativeUrl, Dictionary<string, string> parameters, string body) => !MethodMap.ContainsKey(RelativeUrl)
                ? throw new ArgumentException($"Method {RelativeUrl} not found")
                : MethodMap[RelativeUrl](parameters, body);

        /// <summary>
        /// Performs method registration with known url
        /// </summary>
        /// <param name="RelativeUrl">URL</param>
        /// <param name="RequestProcessorDelegate">Method delegate</param>
        public void RegisterMethod(string RelativeUrl, Func<Dictionary<string, string>, string, string> RequestProcessorDelegate)
        {
            lock (Lock)//новые методу регаются редко, так что простого лока хватит
            {
                if (MethodMap.ContainsKey(RelativeUrl))
                {
                    l.Error($"Tried to register new method '{RelativeUrl}' but it`s already registered!");
                    throw new Exception($"Method {RelativeUrl} already registered");
                }
                MethodMap["/"+RelativeUrl] = RequestProcessorDelegate;
                l.Trace($"Registered new method '{RelativeUrl}'");
            }
        }

        public void UnregisterMethod(string RelativeUrl)
        {
            lock (Lock)
            {
                if (MethodMap.ContainsKey(RelativeUrl)) MethodMap.Remove(RelativeUrl);
                l.Trace($"Unregistered method '{RelativeUrl}'");
            }
        }



    }
}

