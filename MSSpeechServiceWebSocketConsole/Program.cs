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
using System.Threading.Tasks;
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
                    // If you see an API key below, it's a trial key and will either expire soon or get invalidated. Please get your own key.
                    // Get your own trial key to Bing Speech or the new Speech Service at https://azure.microsoft.com/try/cognitive-services
                    // Create an Azure Cognitive Services Account: https://docs.microsoft.com/azure/cognitive-services/cognitive-services-apis-create-account

                    // DELETE THE NEXT THREE LINE ONCE YOU HAVE OBTAINED YOUR OWN SPEECH API KEY
                    //Console.WriteLine("You forgot to initialize the sample with your own Speech API key. Visit https://azure.microsoft.com/try/cognitive-services to get started.");
                    //Console.ReadLine();
                    //return;
                    // END DELETE
#if USENEWSPEECHSDK
                    bool useClassicBingSpeechService = false;
                    //string authenticationKey = @"INSERT-YOUR-NEW-SPEECH-API-KEY-HERE";
                    string authenticationKey = @"895664ef53e44b6fac574c3ecd6f3b75";
#else
                    bool useClassicBingSpeechService = true;
                    //string authenticationKey = @"INSERT-YOUR-BING-SPEECH-API-KEY-HERE";
                    string authenticationKey = @"4d5a1beefe364f8986d63a877ebd51d5";
#endif

                     var recoServiceClient = new SpeechRecognitionClient(useClassicBingSpeechService);
                    // Replace this with your own file. Add it to the project and mark it as "Content" and "Copy if newer".
                    string audioFilePath = @"Thisisatest.wav";

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
