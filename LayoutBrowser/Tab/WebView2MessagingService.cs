﻿using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Monitor.ServiceCommon.Services;
using Monitor.ServiceCommon.Util;
using MonitorCommon;
using Newtonsoft.Json;

namespace LayoutBrowser.Tab
{
    public interface IWebView2MessagingServiceFactory
    {
        public WebView2MessagingService ForWebView2(WebView2 webView);
    }

    public class WebView2MessagingService : IDisposable
    {
        private readonly WebView2 webView;
        private readonly ILogger logger;
        private readonly JsonSerializer ser;

        private readonly ConcurrentDictionary<string, IMessageHandler> handlers = new ConcurrentDictionary<string, IMessageHandler>();

        public WebView2MessagingService(WebView2 webView, JsonSerializerSvc serSvc, ILogger logger)
        {
            this.webView = webView;
            this.logger = logger;

            ser = serSvc.Serializer;

            webView.WebMessageReceived += OnMessageReceived;
        }

        public void AddMessageHandler<TMessage>(string type, Action<TMessage> handler)
        {
            if (!handlers.TryAdd(type, new MessageHandler<TMessage>(handler, ser, e => { logger.LogDebug(e, $"Error handling message of type {type}"); })))
            {
                throw new Exception($"Handler for message type {type} already exists");
            }
        }

        private void OnMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        { 
            string json = e.WebMessageAsJson;

            MessageTypeModel type = ser.Deserialize<MessageTypeModel>(json);

            if (type.type.IsNullOrEmpty())
            {
                return;
            }

            if (handlers.TryGetValue(type.type, out IMessageHandler handler))
            {
                handler.HandleMessage(json);
            }
        }

        public void Dispose()
        {
            webView.WebMessageReceived -= OnMessageReceived;
        }
    }

    public class MessageTypeModel
    {
        public string type;
    }

    public interface IMessageHandler
    {
        public void HandleMessage(string json);
    }

    public class MessageHandler<TMessage> : IMessageHandler
    {
        private readonly Action<TMessage> handler;
        private readonly JsonSerializer ser;
        private readonly Action<Exception> logException;

        public MessageHandler(Action<TMessage> handler, JsonSerializer ser, Action<Exception> logException)
        {
            this.handler = handler;
            this.ser = ser;
            this.logException = logException;
        }

        public void HandleMessage(string json)
        {
            try
            {
                TMessage msg = ser.Deserialize<TMessage>(json);

                handler(msg);
            }
            catch (Exception e)
            {
                logException(e);
            }
        }
    }
}