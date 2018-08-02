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
using SpeechRecognitionService;

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
                    var recoServiceClient = new SpeechRecognitionClient();
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

                    // Register an event to capture recognition events
                    recoServiceClient.OnMessageReceived += RecoServiceClient_OnMessageReceived;

                    await recoServiceClient.CreateSpeechRecognitionJob(audioFilePath, authenticationKey, region);
                }).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception occurred in the main program:" + Environment.NewLine + ex.Message);
            }
        }

        private static void RecoServiceClient_OnMessageReceived(SpeechServiceResult result)
        {
            // Let's ignore all hypotheses and other messages for now and only report back on the final phrase
            if (result.Path == SpeechServiceResult.SpeechMessagePaths.SpeechPhrase)
            {
                Console.WriteLine("*================================================================================");
                Console.WriteLine("* RECOGNITION STATUS: " + result.Result.RecognitionStatus);
                Console.WriteLine("* FINAL RESULT: " + result.Result.DisplayText);
                Console.WriteLine("*================================================================================" + Environment.NewLine);
            }
        }
    }
}
