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
#define USENEWSPEECHSDK

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
            try
            {
                Task.Run(async () =>
                {
                    var recoService = new SpeechRecognitionService();
                    // Replace this with your own file. Add it to the project and mark it as "Content" and "Copy if newer".
                    string audioFilePath = @"Thisisatest.wav";

                    // The key below is a trial key and will either expire soon or get invalidated. Please get your own key.
                    // Get your own trial key to Bing Speech or the new Speech Service at https://azure.microsoft.com/try/cognitive-services
                    // Create an Azure Cognitive Services Account: https://docs.microsoft.com/azure/cognitive-services/cognitive-services-apis-create-account
#if USENEWSPEECHSDK
                    string authenticationKey = @"f69d77d425e946e69a954c53db135f77";
#else
                    string authenticationKey = @"8bd450f1edc143febd45c28d85c3ee7d";
#endif
                    // Make sure to match the region to the Azure region where you created the service.
                    // Note the region is NOT used for the old Bing Speech service
                    string region = "westus";

                    await recoService.CreateSpeechRecognitionJob(audioFilePath, authenticationKey, region);
                }).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred in the main program:" + Environment.NewLine + ex.Message);
            }
        }
    }

    public class SpeechRecognitionService
    {
        public async Task<int?> CreateSpeechRecognitionJob(string audioFilePath, string authenticationKeyStr, string region)
        {
            try
            {
                var authenticationKey = new CogSvcSocketAuthentication(authenticationKeyStr, region);
                string token = authenticationKey.GetAccessToken();

                // Configuring Speech Service Web Socket client header
                Console.WriteLine("Connecting to Speech Service via Web Socket.");
                ClientWebSocket websocketClient = new ClientWebSocket();
                string connectionId = Guid.NewGuid().ToString("N");
                // Make sure to change the region & culture to match your recorded audio file.
                string lang = "en-US";
                websocketClient.Options.SetRequestHeader("X-ConnectionId", connectionId);
                websocketClient.Options.SetRequestHeader("Authorization", "Bearer " + token);

                // Clients must use an appropriate endpoint of Speech Service. The endpoint is based on recognition mode and language.
                // The supported recognition modes are:
                //  - interactive
                //  - conversation
                //  - dictation
#if USENEWSPEECHSDK
                // New Speech Service endpoint. 
                var url = $"wss://{region}.stt.speech.microsoft.com/speech/recognition/interactive/cognitiveservices/v1?format=simple&language={lang}";
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
                    dynamic SpeechConfigPayload = new
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

                    byte[] headerBytes;
                    byte[] headerHead;
                    for (int cursor = 0; cursor < audioFileInfo.Length; cursor++)
                    {
                        headerBytes = BuildAudioHeader(requestId);
                        headerHead = CreateAudioHeaderHead(headerBytes);

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
                    // Send an audio message with a zero-length body. This message tells the service that the client knows
                    // that the user stopped speaking, the utterance is finished, and the microphone is turned off
                    headerBytes = BuildAudioHeader(requestId);
                    headerHead = CreateAudioHeaderHead(headerBytes);
                    var arrEnd = headerHead.Concat(headerBytes).ToArray();
                    await websocketClient.SendAsync(new ArraySegment<byte>(arrEnd, 0, arrEnd.Length), WebSocketMessageType.Binary, true, new CancellationToken());
                    audioFileStream.Dispose();
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
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred during creation of Speech Recognition job:" + Environment.NewLine + ex.Message);
                return null;
            }
        }

        private byte[] BuildAudioHeader(string requestid)
        {
            StringBuilder speechMsgBuilder = new StringBuilder();
            // Clients use the audio message to send an audio chunk to the service.
            speechMsgBuilder.Append("path:audio" + Environment.NewLine);
            speechMsgBuilder.Append($"x-requestid:{requestid}" + Environment.NewLine);
            speechMsgBuilder.Append($"x-timestamp:{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK")}" + Environment.NewLine);
            speechMsgBuilder.Append($"content-type:audio/x-wav");

            return Encoding.ASCII.GetBytes(speechMsgBuilder.ToString());
        }

        private byte[] CreateAudioHeaderHead(byte[] headerBytes)
        {
            var headerbuffer = new ArraySegment<byte>(headerBytes, 0, headerBytes.Length);
            var str = "0x" + (headerBytes.Length).ToString("X");
            var headerHeadBytes = BitConverter.GetBytes((UInt16)headerBytes.Length);
            var isBigEndian = !BitConverter.IsLittleEndian;
            var headerHead = !isBigEndian ? new byte[] { headerHeadBytes[1], headerHeadBytes[0] } : new byte[] { headerHeadBytes[0], headerHeadBytes[1] };
            return headerHead;
        }

        // Allows the WebSocket client to receive messages in a background task
        private static async Task Receiving(ClientWebSocket client)
        {
            try
            {
                var buffer = new byte[512];
                bool isReceiving = true;

                while (isReceiving)
                {

                    var wsResult = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    var resStr = Encoding.UTF8.GetString(buffer, 0, wsResult.Count) +
                        Environment.NewLine + "*** Message End ***" + Environment.NewLine;

                    switch (wsResult.MessageType)
                    {
                        // Incoming text messages can be hypotheses about the words the service recognized or the final
                        // phrase, which is a recognition result that won't change.
                        case WebSocketMessageType.Text:
                            Console.WriteLine(resStr); //Encoding.UTF8.GetString(buffer, 0, wsResult.Count));
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
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred while receiving a message:" + Environment.NewLine + ex.Message);
            }
        }
        public static UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        // 
        public class CogSvcSocketAuthentication
        {
            public static string AuthenticationUri;
            private string subscriptionKey;
            private string token;
            private Timer accessTokenRenewer;

            //Access token expires every 10 minutes. Renew it every 9 minutes.
            private const int RefreshTokenDuration = 9;

            public CogSvcSocketAuthentication(string subscriptionKey, string region)
            {
                try
                {
                    // Important: The Bing Speech service and the new Speech Service DO NOT use the same Uri
#if USENEWSPEECHSDK
                    AuthenticationUri = $"https://{region}.api.cognitive.microsoft.com/sts/v1.0";
#else
                    // The region is ignored for the old Bing Speech service
                    AuthenticationUri = "https://api.cognitive.microsoft.com/sts/v1.0";
#endif

                    this.subscriptionKey = subscriptionKey;
                    this.token = FetchToken(AuthenticationUri, subscriptionKey).Result;

                    // Renew the token based on a fixed interval using a Timer
                    accessTokenRenewer = new Timer(new TimerCallback(OnTokenExpiredCallback),
                                                   this,
                                                   TimeSpan.FromMinutes(RefreshTokenDuration),
                                                   TimeSpan.FromMilliseconds(-1));

                }
                catch (Exception ex)
                {
                    Console.WriteLine("An exception occurred during authentication:" + Environment.NewLine + ex.Message);
                }
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
