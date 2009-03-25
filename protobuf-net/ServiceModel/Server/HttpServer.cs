﻿#if !SILVERLIGHT && !CF

using System;
using System.Net;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using ProtoBuf.ServiceModel.Client;
#if NET_3_0
using System.ServiceModel;
#endif
using System.Text;

namespace ProtoBuf.ServiceModel.Server
{
    /// <summary>
    /// Standalone http server compatible with <seealso cref="ProtoBuf.ServiceModel.Client.HttpBasicTransport"/>.
    /// </summary>
    public class HttpServer : IDisposable
    {
        private readonly Type serviceContractType;
        private readonly Type serviceImplementationType;
        private object serviceSingleton;
        private Dictionary<string, MethodInfo> actions;
        private Uri uriPrefix;
        private HttpListener listener;
        /// <summary>
        /// Create a new HttpServer instance for the given service-type.
        /// </summary>
        /// <param name="uriPrefix">The base uri on which to listen for messages.</param>
        /// <param name="serviceContractType">The interface that represents the service contract.</param>
        /// <param name="serviceImplementationType">The concrete type that implements the service contract.</param>
        public HttpServer(string uriPrefix, Type serviceContractType, Type serviceImplementationType)
        {
            if (serviceContractType == null) throw new ArgumentNullException("serviceContractType");
            if (serviceImplementationType == null) throw new ArgumentNullException("serviceImplementationType");
            if (!serviceContractType.IsInterface)
            {
                throw new InvalidOperationException(serviceContractType.Name + " is not an interface");
            }
            this.serviceContractType = serviceContractType;
            this.serviceImplementationType = serviceImplementationType;
            if (string.IsNullOrEmpty(uriPrefix)) throw new ArgumentNullException("uriPrefix");
            this.uriPrefix = new Uri(uriPrefix);
            listener = new HttpListener();
            listener.Prefixes.Add(uriPrefix);
            gotContext = GotContext;

            actions = new Dictionary<string, MethodInfo>(StringComparer.InvariantCulture);
            foreach (MethodInfo method in serviceContractType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.IsGenericMethod || method.IsGenericMethodDefinition) continue;
                string key = RpcUtils.ResolveActionStandard(method);
                if (actions.ContainsKey(key))
                {
                    throw new ArgumentException("Duplicate action \"" + key + "\" found on service-contract " + serviceContractType.FullName, "serviceContractType");
                }
                actions.Add(key, method);
            }
        }

        private static Type GetType(object serviceSingleton)
        {
            if (serviceSingleton == null) throw new ArgumentNullException("serviceSingleton");
            return serviceSingleton.GetType();
        }
        /// <summary>
        /// Create a new HttpServer instance for the given service-type.
        /// </summary>
        /// <param name="uriPrefix">The base uri on which to listen for messages.</param>
        /// <param name="serviceContractType">The interface that represents the service contract.</param>
        /// <param name="serviceSingleton">The fixed instance that implements the service contract. The server assumes
        /// ownership of this singleton: if appropriate, it will be disposed with the server.</param>
        /// <remarks>Note that </remarks>
        public HttpServer(string uriPrefix, Type serviceContractType, object serviceSingleton)
            : this(uriPrefix, serviceContractType, GetType(serviceSingleton))
        {
            this.serviceSingleton = serviceSingleton;
        }
        private void CheckDisposed()
        {
            if (listener == null) throw new ObjectDisposedException(ToString());
        }
        /// <summary>
        /// Begin listening for messages on the server.
        /// </summary>
        public void Start()
        {
            CheckDisposed();
            if (!listener.IsListening)
            {
                Trace.WriteLine("Starting server on " + uriPrefix);
                listener.Start();
                Trace.WriteLine("(started)");
                ListenForContext();
            }
        }
        Action<HttpListenerContext> gotContext;

        /// <summary>
        /// Identify the method that implements a given action.
        /// </summary>
        /// <param name="action">The action in the request.</param>
        /// <returns>The method that should implement the action.</returns>
        protected virtual MethodInfo ResolveMethod(string action)
        {
            MethodInfo method;
            actions.TryGetValue(action, out method);
            return method;
        }

        private void ProcessContext(HttpListenerContext context)
        {
            string rpcVer = context.Request.Headers[RpcUtils.HTTP_RPC_VERSION_HEADER];
            if (!string.IsNullOrEmpty(rpcVer) && rpcVer != "0.1")
            {
                throw new InvalidOperationException("Incorrect RPC version");
            }
            
            string action = uriPrefix.MakeRelativeUri(context.Request.Url).ToString();
            MethodInfo method = ResolveMethod(action);
            if (method == null) throw new InvalidOperationException("Action not found: " + action);
            if (method.IsGenericMethod || method.IsGenericMethodDefinition) throw new InvalidOperationException("Cannot process generic method: " + method.Name);
            if (method.DeclaringType != serviceContractType) throw new ArgumentException(method.Name + " is not defined on service " + serviceContractType.FullName, "method");
            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];

            RpcUtils.UnpackArgs(context.Request.InputStream, method, args, RpcUtils.IsRequestArgument);
            
            bool createObj = serviceSingleton != null;
            object serviceInstance = createObj ? Activator.CreateInstance(serviceImplementationType) : serviceSingleton;
            try
            {
                object responseObj = method.Invoke(serviceInstance, args);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = RpcUtils.HTTP_RPC_MIME_TYPE;
                RpcUtils.PackArgs(context.Response.OutputStream, method, responseObj, args, RpcUtils.IsResponseArgument);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, GetType() + ":" + serviceContractType.Name);
                throw;
            }
            finally // release the singleton if we own it...
            {
                if (createObj) Dispose(ref serviceInstance);
            }
        }
        static void Dispose<T>(ref T obj) where T : class
        { // note no IDisposable constraint; we're not always sure if
            // the item is disposable...
            if (obj != null && obj is IDisposable)
            {
                try { ((IDisposable)obj).Dispose(); }
                catch (Exception ex)
                { // log only
                    Trace.Write(ex, "HttpServer.Dispose");
                }
            }
            obj = null;
        }
        private void GotContext(HttpListenerContext context)
        {
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest; // assume failure...
                ProcessContext(context);
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.ContentType = "text/plain";
                    byte[] buffer = Encoding.UTF8.GetBytes(ex.Message);
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                catch { }
            }
            finally
            {
                try
                {
                    context.Response.Close();
                }
                catch { }
                try
                {
                    ListenForContext();
                }
                catch { }
            }
        }
        private void ListenForContext() {
            AsyncUtility.RunAsync(
                    listener.BeginGetContext, listener.EndGetContext,
                    gotContext, null);
        }
        /// <summary>
        /// Stop listening for messages on the server, and release
        /// any associated resources.
        /// </summary>
        public void Close()
        {
            if (listener != null)
            {
                Trace.WriteLine("Stopping server on " + uriPrefix);
                listener.Close();
                Trace.WriteLine("(stopped)");
                Dispose(ref listener);
            }
            Dispose(ref serviceSingleton);
            gotContext = null;
        }
        void IDisposable.Dispose() { Close(); }
    }
}
#endif