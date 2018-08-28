/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ExchangeSharp;

namespace ExchangeSharpConsole
{
    public class WebSocket4NetClientWebSocket : ExchangeSharp.ClientWebSocket.IClientWebSocketImplementation
    {
        private class QueuedWebSocketMessage
        {
            public byte[] Data { get; set; }
            public int Index { get; set; }
            public int Length { get; set; }
            public bool IsBinary { get; set; }
        }

        private readonly ConcurrentQueue<QueuedWebSocketMessage> messages = new ConcurrentQueue<QueuedWebSocketMessage>();
        private readonly AutoResetEvent messageEvent = new AutoResetEvent(false);
        private readonly MemoryStream sendMessage = new MemoryStream();

        private WebSocket4Net.WebSocket webSocket;

        public WebSocketState State
        {
            get
            {
                if (webSocket == null)
                {
                    return WebSocketState.Closed;
                }
                switch (webSocket.State)
                {
                    case WebSocket4Net.WebSocketState.Closing:
                        return WebSocketState.CloseSent;

                    case WebSocket4Net.WebSocketState.Connecting:
                        return WebSocketState.Connecting;

                    case WebSocket4Net.WebSocketState.Open:
                        return WebSocketState.Open;

                    default:
                        return WebSocketState.Closed;
                }
            }
        }

        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15.0);

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            webSocket?.Close((ushort)closeStatus, statusDescription);
            return Task.CompletedTask;
        }

        public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            webSocket = new WebSocket4Net.WebSocket(uri.ToString())
            {
                AutoSendPingInterval = (int)KeepAliveInterval.TotalMilliseconds,
                EnableAutoSendPing = true
            };
            webSocket.DataReceived += WebSocket_OnData;
            webSocket.MessageReceived += WebSocket_OnMessage;
            webSocket.Open();
            return Task.CompletedTask;
        }

        private void WebSocket_OnData(object sender, WebSocket4Net.DataReceivedEventArgs e)
        {
            messages.Enqueue(new QueuedWebSocketMessage { Data = e.Data, IsBinary = true });
            messageEvent.Set();
        }

        private void WebSocket_OnMessage(object sender, WebSocket4Net.MessageReceivedEventArgs e)
        {
            messages.Enqueue(new QueuedWebSocketMessage { Data = Encoding.UTF8.GetBytes(e.Message), IsBinary = false });
            messageEvent.Set();
        }

        public void Dispose()
        {
            webSocket?.Close();
            webSocket = null;
            sendMessage.SetLength(0);
        }

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            while (true)
            {
                messageEvent.WaitOne(100);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                else if (webSocket.State == WebSocket4Net.WebSocketState.Closed || webSocket.State == WebSocket4Net.WebSocketState.Closing)
                {
                    return Task.FromResult<WebSocketReceiveResult>(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
                }
                while (messages.TryPeek(out QueuedWebSocketMessage result))
                {
                    // fill up the buffer
                    result.Length = Math.Min(buffer.Count, result.Data.Length - result.Index);
                    Array.Copy(result.Data, result.Index, buffer.Array, 0, result.Length);
                    result.Index += result.Length;
                    WebSocketMessageType type = (result.IsBinary ? WebSocketMessageType.Binary : WebSocketMessageType.Text);
                    if (result.Index == result.Data.Length)
                    {
                        // remove the message
                        messages.TryDequeue(out _);

                        // message complete
                        return Task.FromResult<WebSocketReceiveResult>(new WebSocketReceiveResult(result.Length, type, true, null, null));
                    }
                    else
                    {
                        // partial message, more coming
                        return Task.FromResult<WebSocketReceiveResult>(new WebSocketReceiveResult(result.Length, type, false, null, null));
                    }
                }
            }
            return null;
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            sendMessage.Write(buffer.Array, buffer.Offset, buffer.Count);
            if (endOfMessage)
            {
                if (messageType == WebSocketMessageType.Binary)
                {
                    webSocket?.Send(sendMessage.GetBuffer(), 0, (int)sendMessage.Length);
                }
                else
                {
                    webSocket?.Send(Encoding.UTF8.GetString(sendMessage.GetBuffer(), 0, (int)sendMessage.Length));
                }
                sendMessage.SetLength(0);
            }
            return Task.CompletedTask;
        }
    }
}
