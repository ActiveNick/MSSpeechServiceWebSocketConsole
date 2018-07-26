//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
//
// Microsoft Cognitive Services (formerly Project Oxford): 
// https://www.microsoft.com/cognitive-services
//
// New Speech Service: 
// https://docs.microsoft.com/en-us/azure/cognitive-services/Speech-Service/
// Old Bing Speech SDK: 
// https://docs.microsoft.com/en-us/azure/cognitive-services/Speech/home
//
// Copyright (c) Microsoft Corporation
// All rights reserved.
//
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Comment the following line if you want to use the old Bing Speech SDK
// instead of the new Speech Service.
//#define USENEWSPEECHSDK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

// Bing Speech API Web Socket protocol documentation available at
// https://docs.microsoft.com/en-us/azure/cognitive-services/speech/api-reference-rest/websocketprotocol.
// To use with the new Speech Service, please refer to the REST APIs documentation at
// https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/rest-apis.

namespace MSSpeechServiceWebSocketConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(async () =>
            {
                var recoService = new SpeechRecognitionService();
                // Replace this with your own file. Add it to the project and mark it as "Content" and "Copy if newer".
                var audioFilePath = @"Thisisatest.wav";

                // The key below is a trial key and will either expire soon or get invalidated. Please get your own key.
                // Get your own trial key to Bing Speech or the new Speech Service at https://azure.microsoft.com/try/cognitive-services
                // Create an Azure Cognitive Services Account: https://docs.microsoft.com/azure/cognitive-services/cognitive-services-apis-create-account
                var authenticationKey = @"8bd450f1edc143febd45c28d85c3ee7d";

                await recoService.CreateSpeechRecognitionJob(audioFilePath, authenticationKey);
            }).Wait();
        }
    }

    public class SpeechRecognitionService
    {
        public async Task<int?> CreateSpeechRecognitionJob(string audioFilePath, string authenticationKeyStr)
        {
            var authenticationKey = new CogSvcSocketAuthentication(authenticationKeyStr);
            string token = authenticationKey.GetAccessToken();

            // Configuring Speech Service Web Socket client header
            Console.WriteLine("Connecting to Speech Service Web Socket.");
            ClientWebSocket websocketClient = new ClientWebSocket();
            var connectionId = Guid.NewGuid().ToString("N");
            var lang = "en-US";
            websocketClient.Options.SetRequestHeader("X-ConnectionId", connectionId);
            websocketClient.Options.SetRequestHeader("Authorization", "Bearer " + token);

            // Clients must use an appropriate endpoint of Speech Service. The endpoint is based on recognition mode and language.
            // The supported recognition modes are:
            //  - interactive
            //  - conversation
            //  - dictation
#if USENEWSPEECHSDK
            // New Speech Service endpoint.
            var url = $"wss://westus.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?format=simple&language={lang}";
#else
            // Bing Speech endpoint
            var url = $"wss://speech.platform.bing.com/speech/recognition/interactive/cognitiveservices/v1?format=simple&language={lang}";
#endif
            await websocketClient.ConnectAsync(new Uri(url), new CancellationToken());
            Console.WriteLine("Web Socket successfully connected.");

            var receiving = Receiving(websocketClient);

            var sending = Task.Run(async () =>
            {
                // CONFIGURING SPEECH SERVICE:
                // The payload of the speech.config message is a JSON structure
                // that contains information about the application.
                dynamic SpeechConfigPayload =
                    new
                    {
                        context = new
                        {
                            system = new
                            {
                                version = "1.0.00000"
                            },
                            os = new
                            {
                                platform = "Speech Service WebSocket Console App",
                                name = "Sample",
                                version = "1.0.00000"
                            },
                            device = new
                            {
                                manufacturer = "Microsoft",
                                model = "SpeechSample",
                                version = "1.0.00000"
                            }
                        }
                    };

                // Create a unique request ID, must be a UUID in "no-dash" format
                var requestId = Guid.NewGuid().ToString("N");

                // Convert speech.config payload to JSON
                var SpeechConfigPayloadJson = JsonConvert.SerializeObject(SpeechConfigPayload, Formatting.None);

                // Create speech.config message from required headers and JSON payload
                StringBuilder speechMsgBuilder = new StringBuilder();
                speechMsgBuilder.Append("path:speech.config" + Environment.NewLine);
                speechMsgBuilder.Append("x-requestid:" + requestId + Environment.NewLine);
                speechMsgBuilder.Append($"x-timestamp:{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK")}" + Environment.NewLine);
                speechMsgBuilder.Append($"content-type:application/json; charset=utf-8" + Environment.NewLine);
                speechMsgBuilder.Append(Environment.NewLine);
                speechMsgBuilder.Append(SpeechConfigPayloadJson);
                var strh = speechMsgBuilder.ToString();

                var encoded = Encoding.UTF8.GetBytes(speechMsgBuilder.ToString());
                var buffer = new ArraySegment<byte>(encoded, 0, encoded.Length);

                if (websocketClient.State != WebSocketState.Open) return;

                Console.WriteLine("Sending speech.config...");
                // Send speech.config to Speech Service
                await websocketClient.SendAsync(buffer, WebSocketMessageType.Text, true, new CancellationToken());
                Console.WriteLine("speech.config sent successfully!");

                // SENDING AUDIO TO SPEECH SERVICE:
                // Speech-enabled client applications send audio to Speech Service by converting the audio stream
                // into a series of audio chunks. Each chunk of audio carries a segment of the spoken audio that's
                // to be transcribed by the service. The maximum size of a single audio chunk is 8,192 bytes.
                // Audio stream messages are Binary WebSocket messages.
                Console.WriteLine($"Preparing to send audio file: {audioFilePath}");
                FileInfo audioFileInfo = new FileInfo(audioFilePath);
                FileStream audioFileStream = audioFileInfo.OpenRead();

                for (int cursor = 0; cursor < audioFileInfo.Length; cursor++)
                {
                    // Clients use the audio message to send an audio chunk to the service.
                    speechMsgBuilder.Clear();
                    speechMsgBuilder.Append("path:audio" + Environment.NewLine);
                    speechMsgBuilder.Append($"x-requestid:{requestId}" + Environment.NewLine);
                    speechMsgBuilder.Append($"x-timestamp:{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK")}" + Environment.NewLine);
                    speechMsgBuilder.Append($"content-type:audio/x-wav");

                    var headerBytes = Encoding.ASCII.GetBytes(speechMsgBuilder.ToString());
                    var headerbuffer = new ArraySegment<byte>(headerBytes, 0, headerBytes.Length);
                    var str = "0x" + (headerBytes.Length).ToString("X");
                    var headerHeadBytes = BitConverter.GetBytes((UInt16)headerBytes.Length);
                    var isBigEndian = !BitConverter.IsLittleEndian;
                    var headerHead = !isBigEndian ? new byte[] { headerHeadBytes[1], headerHeadBytes[0] } : new byte[] { headerHeadBytes[0], headerHeadBytes[1] };

                    // PCM audio must be sampled at 16 kHz with 16 bits per sample and one channel (riff-16khz-16bit-mono-pcm).
                    var byteLen = 8192 - headerBytes.Length - 2;
                    var fbuff = new byte[byteLen];
                    audioFileStream.Read(fbuff, 0, byteLen);

                    var arr = headerHead.Concat(headerBytes).Concat(fbuff).ToArray();
                    var arrSeg = new ArraySegment<byte>(arr, 0, arr.Length);

                    Console.WriteLine($"Sending audio data from position: {cursor}");
                    if (websocketClient.State != WebSocketState.Open) return;
                    cursor += byteLen;
                    var end = cursor >= audioFileInfo.Length;
                    await websocketClient.SendAsync(arrSeg, WebSocketMessageType.Binary, true, new CancellationToken());
                    Console.WriteLine($"Audio data from file {audioFilePath} sent successfully!");

                    var dt = Encoding.ASCII.GetString(arr);
                }
                await websocketClient.SendAsync(new ArraySegment<byte>(), WebSocketMessageType.Binary, true, new CancellationToken());
                audioFileStream.Dispose();

                {
                    var startWait = DateTime.UtcNow;
                    while ((DateTime.UtcNow - startWait).TotalSeconds < 30)
                    {
                        await Task.Delay(1);
                    }
                    if (websocketClient.State != WebSocketState.Open) return;
                }
            });

            // Wait for tasks to complete
            await Task.WhenAll(sending, receiving);
            if (sending.IsFaulted)
            {
                var err = sending.Exception;
                throw err;
            }
            if (receiving.IsFaulted)
            {
                var err = receiving.Exception;
                throw err;
            }

            return null;
        }

        // Allows the WebSocket client to receive messages in a background task
        private static async Task Receiving(ClientWebSocket client)
        {
            var buffer = new byte[128];
            bool isReceiving = true;

            while (isReceiving)
            {

                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                var res = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Incoming text messages can be hypotheses about the words the service recognized or the final
                // phrase, which is a recognition result that won't change.
                switch (result.MessageType)
                {
                    case WebSocketMessageType.Text:
                        Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        break;
                    case WebSocketMessageType.Binary:
                        Console.WriteLine("Binary messages are not suppported by this application.");
                        break;
                    case WebSocketMessageType.Close:
                        string description = client.CloseStatusDescription;
                        Console.WriteLine($"Closing WebSocket with Status: {description}");
                        //await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        isReceiving = false;
                        break;
                    default:
                        Console.WriteLine("The message type was not recognized.");
                        break;
                }
            }
        }
        public static UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        // 
        public class CogSvcSocketAuthentication
        {
            // This is the same Uri for the Bing Speech service and the new Speech Service
            public static readonly string AuthenticationUri = "https://api.cognitive.microsoft.com/sts/v1.0";
            private string subscriptionKey;
            private string token;
            private Timer accessTokenRenewer;

            //Access token expires every 10 minutes. Renew it every 9 minutes.
            private const int RefreshTokenDuration = 9;

            public CogSvcSocketAuthentication(string subscriptionKey)
            {
                this.subscriptionKey = subscriptionKey;
                this.token = FetchToken(AuthenticationUri, subscriptionKey).Result;

                // Renew the token based on a fixed interval using a Timer
                accessTokenRenewer = new Timer(new TimerCallback(OnTokenExpiredCallback),
                                               this,
                                               TimeSpan.FromMinutes(RefreshTokenDuration),
                                               TimeSpan.FromMilliseconds(-1));
            }

            public string GetAccessToken()
            {
                return this.token;
            }

            private void RenewAccessToken()
            {
                this.token = FetchToken(AuthenticationUri, this.subscriptionKey).Result;
                Console.WriteLine("Renewed authentication token.");
            }

            private void OnTokenExpiredCallback(object stateInfo)
            {
                try
                {
                    RenewAccessToken();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Failed renewing access token. Details: {0}", ex.Message));
                }
                finally
                {
                    try
                    {
                        accessTokenRenewer.Change(TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("Failed to reschedule the timer to renew access token. Details: {0}", ex.Message));
                    }
                }
            }

            private async Task<string> FetchToken(string fetchUri, string subscriptionKey)
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                    UriBuilder uriBuilder = new UriBuilder(fetchUri);
                    uriBuilder.Path += "/issueToken";

                    var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null);
                    Console.WriteLine("Token Uri: {0}", uriBuilder.Uri.AbsoluteUri);
                    return await result.Content.ReadAsStringAsync();
                }
            }
        }
    }

}
