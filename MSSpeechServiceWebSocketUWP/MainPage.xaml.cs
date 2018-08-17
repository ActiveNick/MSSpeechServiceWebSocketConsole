using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SpeechRecognitionService;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MSSpeechServiceWebSocketUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            // If you see an API key below, it's a trial key and will either expire soon or get invalidated. Please get your own key.
            // Get your own trial key to Bing Speech or the new Speech Service at https://azure.microsoft.com/try/cognitive-services
            // Create an Azure Cognitive Services Account: https://docs.microsoft.com/azure/cognitive-services/cognitive-services-apis-create-account

            // DELETE THE NEXT THREE LINE ONCE YOU HAVE OBTAINED YOUR OWN SPEECH API KEY
            //Console.WriteLine("You forgot to initialize the sample with your own Speech API key. Visit https://azure.microsoft.com/try/cognitive-services to get started.");
            //Console.ReadLine();
            //return;
            // END DELETE
            //#if USENEWSPEECHSDK
            //                    bool useClassicBingSpeechService = false;
            //                    //string authenticationKey = @"INSERT-YOUR-NEW-SPEECH-API-KEY-HERE";
            //                    string authenticationKey = @"f69d77d425e946e69a954c53db135f77";
            //#else
            //            bool useClassicBingSpeechService = true;
            //            //string authenticationKey = @"INSERT-YOUR-BING-SPEECH-API-KEY-HERE";
            //            string authenticationKey = @"4d5a1beefe364f8986d63a877ebd51d5";
            //#endif
            bool useClassicBingSpeechService = false;
            string authenticationKey = txtSubscriptionKey.Text;

            var recoServiceClient = new SpeechRecognitionClient(useClassicBingSpeechService);
            // Replace this with your own file. Add it to the project and mark it as "Content" and "Copy if newer".
            string audioFilePath = txtFilename.Text;

            // Make sure to match the region to the Azure region where you created the service.
            // Note the region is NOT used for the old Bing Speech service
            string region = txtRegion.Text;

            // Register an event to capture recognition events
            recoServiceClient.OnMessageReceived += RecoServiceClient_OnMessageReceived;

            recoServiceClient.CreateSpeechRecognitionJob(audioFilePath, authenticationKey, region);

            lblResult.Text = "Speech recognition job started... uploading audio file. Please wait for first result...";
        }

        private async void RecoServiceClient_OnMessageReceived(SpeechServiceResult result)
        {
            // Let's ignore all hypotheses and other messages for now and only report back on the final phrase
            if (result.Path == SpeechServiceResult.SpeechMessagePaths.SpeechHypothesis)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    lblResult.Text = "SPEECH HYPOTHESIS RETURNED: " + Environment.NewLine;
                    lblResult.Text += result.Result.Text;
                });
            }
            else if (result.Path == SpeechServiceResult.SpeechMessagePaths.SpeechPhrase)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    lblResult.Text = "RECOGNITION STATUS: " + result.Result.RecognitionStatus + Environment.NewLine;
                    lblResult.Text += "FINAL RESULT: " + result.Result.DisplayText + Environment.NewLine;
                });
            }
        }

    }
}
