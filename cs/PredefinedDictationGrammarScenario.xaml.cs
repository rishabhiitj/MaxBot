using SDKTemplate;
using System;
using Windows.Devices.SerialCommunication;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Web.Http.Filters;
using System.Threading;

using Windows.Web.Http;
using System.Diagnostics;
using Windows.Security.Cryptography.Certificates;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SpeechToText;

namespace SpeechAndTTS
{
    public sealed partial class PredefinedDictationGrammarScenario : Page
    {
        /// <summary>
        /// This HResult represents the scenario where a user is prompted to allow in-app speech, but 
        /// declines. This should only happen on a Phone device, where speech is enabled for the entire device,
        /// not per-app.
        /// </summary>
        private static uint HResultPrivacyStatementDeclined = 0x80045509;

        private SpeechRecognizer speechRecognizer;
        private CoreDispatcher dispatcher;
        private IAsyncOperation<SpeechRecognitionResult> recognitionOperation;
        private ResourceContext speechContext;
        private ResourceMap speechResourceMap;

        //
        private HttpBaseProtocolFilter filter;
        private HttpClient httpClient;
        private CancellationTokenSource cts;
        private bool isFilterUsed;
        private string r;
        private string s;

        public TextBox OutputField = new TextBox();
        private Button btnRecognizeWithUI = new Button();

        public PredefinedDictationGrammarScenario()
        {
            InitializeComponent();
        }

        /// <summary>
        /// When activating the scenario, ensure we have permission from the user to access their microphone, and
        /// provide an appropriate path for the user to enable access to the microphone if they haven't
        /// given explicit permission for it.
        /// </summary>
        /// <param name="e">The navigation event details</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Save the UI thread dispatcher to allow speech status messages to be shown on the UI.
            filter = new HttpBaseProtocolFilter();
            filter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;
            httpClient = new HttpClient(filter);
            cts = new CancellationTokenSource();
            isFilterUsed = false;

            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {
                // Enable the recognition buttons.
                btnRecognizeWithUI.IsEnabled = true;
                btnRecognizeWithoutUI.IsEnabled = true;
                
                Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
                speechContext = ResourceContext.GetForCurrentView();
                speechContext.Languages = new string[] { speechLanguage.LanguageTag };

                speechResourceMap = ResourceManager.Current.MainResourceMap.GetSubtree("LocalizationSpeechResources");

                PopulateLanguageDropdown();
                //await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            }
            else
            {
                resultTextBlock.Visibility = Visibility.Visible;
                resultTextBlock.Text = "Permission to access capture resources was not given by the user; please set the application setting in Settings->Privacy->Microphone.";
                btnRecognizeWithUI.IsEnabled = false;
                btnRecognizeWithoutUI.IsEnabled = false;
                cbLanguageSelection.IsEnabled = false;

            }
        }

        /// <summary>
        /// Look up the supported languages for this speech recognition scenario, 
        /// that are installed on this machine, and populate a dropdown with a list.
        /// </summary>
        private void PopulateLanguageDropdown()
        {
            Language defaultLanguage = SpeechRecognizer.SystemSpeechLanguage;
            IEnumerable<Language> supportedLanguages = SpeechRecognizer.SupportedTopicLanguages;
            foreach (Language lang in supportedLanguages)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Tag = lang;
                item.Content = lang.DisplayName;

                cbLanguageSelection.Items.Add(item);
                if (lang.LanguageTag == defaultLanguage.LanguageTag)
                {
                    item.IsSelected = true;
                    cbLanguageSelection.SelectedItem = item;
                }
            }
        }

        /// <summary>
        /// When a user changes the speech recognition language, trigger re-initialization of the 
        /// speech engine with that language, and change any speech-specific UI assets.
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private async void cbLanguageSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (speechRecognizer != null)
            {
                ComboBoxItem item = (ComboBoxItem)(cbLanguageSelection.SelectedItem);
                Language newLanguage = (Language)item.Tag;
                if (speechRecognizer.CurrentLanguage != newLanguage)
                {
                    // trigger cleanup and re-initialization of speech.
                    try
                    {
                        speechContext.Languages = new string[] { newLanguage.LanguageTag };

                        await InitializeRecognizer(newLanguage);
                    }
                    catch (Exception exception)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Ensure that we clean up any state tracking event handlers created in OnNavigatedTo to prevent leaks.
        /// </summary>
        /// <param name="e">Details about the navigation event</param>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (speechRecognizer != null)
            {
                if (speechRecognizer.State != SpeechRecognizerState.Idle)
                {
                    if (recognitionOperation != null)
                    {
                        recognitionOperation.Cancel();
                        recognitionOperation = null;
                    }
                }

                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            if (e.NavigationMode == NavigationMode.Forward && e.Uri == null)
            {
                return;
            }

            Dispose();

        }

        public void Dispose()
        {
            if (filter != null)
            {
                filter.Dispose();
                filter = null;
            }

            if (httpClient != null)
            {
                httpClient.Dispose();
                httpClient = null;
            }

            if (cts != null)
            {
                cts.Dispose();
                cts = null;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            cts.Dispose();

            // Re-create the CancellationTokenSource.
            cts = new CancellationTokenSource();
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
                      
            String strs = "https://api.projectoxford.ai/luis/v1/application?id=703dfe17-d565-490d-be00-910712691577&subscription-key=f43e1e3b89674540a272f41a042107e5&q=" + AddressField.Text;

            string result1=await responses(strs);
            start_result.Visibility = result_TextBlock.Visibility = Visibility.Collapsed;
            //hlOpenPrivacySettings.Visibility = Visibility.Collapsed;

            //recognitionOperation = speechRecognizer.RecognizeWithUIAsync();
            //SpeechRecognitionResult speechRecognitionResult = await recognitionOperation;
            // If successful, display the recognition result.
            //if (speechRecognitionResult.Status == SpeechRecognitionResultStatus.Success)
            //{
            start_result.Visibility = result_TextBlock.Visibility = Visibility.Visible;
            //string result1 = await get_fortune();
                    result_TextBlock.Text = result1;
                //}
                //else
                //{
                //    resultTextBlock.Visibility = Visibility.Visible;
                //    resultTextBlock.Text = string.Format("Speech Recognition Failed, Status: {0}", speechRecognitionResult.Status.ToString());
                //}
            Debug.WriteLine("hi");
            
        }

        public async Task<string> responses(String strs)
        {

            Uri resourceUri;

            // The value of 'AddressField' is set by the user and is therefore untrusted input. If we can't create a
            // valid, absolute URI, we'll notify the user about the incorrect input.
            if (!Helpers.TryGetUri(strs, out resourceUri))
            {
                //rootPage.NotifyUser("Invalid URI.", NotifyType.ErrorMessage);
                return "Incorrect Input";
            }

            Helpers.ScenarioStarted(StartButton, CancelButton, OutputField);
            //rootPage.NotifyUser("In progress", NotifyType.StatusMessage);

            try
            {
                filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
                filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);

                HttpResponseMessage response = await httpClient.GetAsync(resourceUri).AsTask(cts.Token);
                isFilterUsed = true;

                await Helpers.DisplayTextResultAsync(response, OutputField, cts.Token);

                r = await response.Content.ReadAsStringAsync();
                s = r.ToString();
                //Debug.WriteLine(s);

                string[] seperatedWords = s.Split(' ');
                int c = 0;
                int d = 0;
                string intent = "";
                string entity = "";
                intent = parser(seperatedWords, "intent");
                entity = parser(seperatedWords, "type");
                           
                return await final_get(intent, entity);
                //rootPage.NotifyUser(
                //        "Completed. Response came from " + response.Source + ". HTTP version used: " + response.Version.ToString() + ".",
                //        NotifyType.StatusMessage);
            }
            catch (TaskCanceledException)
            {
                return "Notify user";
                //rootPage.NotifyUser("Request canceled.", NotifyType.ErrorMessage);
            }
            catch (Exception ex)
            {
                return "Notify User";
                //rootPage.NotifyUser("Error is : " + ex.Message, NotifyType.ErrorMessage);
            }
            finally
            {
                Helpers.ScenarioCompleted(StartButton, CancelButton);
            }
            
        }

        async Task<string> final_get(String intent, String entity)
        {
            Debug.WriteLine("in final_get");
            String answer;
            switch (intent)
            {
                case "StartActivity":
                    switch (entity)
                    {
                        case "lights":
                            // SerialDevice serial
                            return "lights";                                              
                            break;
                        case "fortune":
                            return await get_fortune();
                            break;
                        case "music":
                            return "Music";
                            break;
                        case "bus":
                            //return get_bus_schedule();
                            return "bus";
                            break;
                        case "mess":
                            return "mess";
                            //return get_messmenu();
                            break;
                        case "joke":
                            return await get_joke();
                            break;
                        case "weather":
                            //Debug.WriteLine("test");
                            return await get_weather();
                            break;
                        default:
                            answer = "Either wrong intent/entity or the user input is still to be trained";
                            return answer;
                            break;
                    }
                    break;
                case "StopActivity":
                    switch (entity)
                    {
                        case "lights":
                            return "light";
                            break;
                        case "music":
                            return "music";
                            break;
                        default:
                            answer = "Either wrong intent/entity or the user input is still to be trained";
                            return answer;
                            break;
                    }
                    break;
                default:
                    answer = intent;
                    return answer;
                    break;
            }
        }


        public async Task<string> get_fortune()
        {
            Debug.WriteLine("in get_fortune");

            //WebRequest req = WebRequest.Create("http://tambal.azurewebsites.net/joke/random");

            //WebResponse rep = await req.GetResponseAsync();

            Uri resourceUri;

            string strs = "http://tambal.azurewebsites.net/joke/random";
            // The value of 'AddressField' is set by the user and is therefore untrusted input. If we can't create a
            // valid, absolute URI, we'll notify the user about the incorrect input.
            if (!Helpers.TryGetUri(strs, out resourceUri))
            {
                //rootPage.NotifyUser("Invalid URI.", NotifyType.ErrorMessage);
                return "Incorrect Input";
            }

            Helpers.ScenarioStarted(StartButton, CancelButton, OutputField);
            //rootPage.NotifyUser("In progress", NotifyType.StatusMessage);


            HttpResponseMessage response = await httpClient.GetAsync(resourceUri).AsTask(cts.Token);
            isFilterUsed = true;

            await Helpers.DisplayTextResultAsync(response, OutputField, cts.Token);

            string r = await response.Content.ReadAsStringAsync();
            string s = r.ToString();
            Debug.WriteLine(s);

            string json = response.Content.ToString();
            var data = (JObject)JsonConvert.DeserializeObject(json);
            string joke_data = data["joke"].Value<string>();

            return(joke_data);
            //Debug.WriteLine(joke_data);

        }

        public void get_messmenu()
        {
            this.Frame.Navigate(typeof(Output));
        }

        public void get_bus_schedule()
        {
            this.Frame.Navigate(typeof(Output));
        }

        public async Task<string> get_joke()
        {

            Debug.WriteLine("in get_joke");

            //WebRequest req = WebRequest.Create("http://tambal.azurewebsites.net/joke/random");

            //WebResponse rep = await req.GetResponseAsync();

            Uri resourceUri;

            string strs = "http://tambal.azurewebsites.net/joke/random";
            // The value of 'AddressField' is set by the user and is therefore untrusted input. If we can't create a
            // valid, absolute URI, we'll notify the user about the incorrect input.
            if (!Helpers.TryGetUri(strs, out resourceUri))
            {
                //rootPage.NotifyUser("Invalid URI.", NotifyType.ErrorMessage);
                return "Incorrect Input";
            }

            Helpers.ScenarioStarted(StartButton, CancelButton, OutputField);
            //rootPage.NotifyUser("In progress", NotifyType.StatusMessage);


            HttpResponseMessage response = await httpClient.GetAsync(resourceUri).AsTask(cts.Token);
            isFilterUsed = true;

            await Helpers.DisplayTextResultAsync(response, OutputField, cts.Token);

            string r = await response.Content.ReadAsStringAsync();
            string s = r.ToString();
            Debug.WriteLine(s);

            string json = response.Content.ToString();
            var data = (JObject)JsonConvert.DeserializeObject(json);
            string joke_data = data["joke"].Value<string>();

            return joke_data;
            //Debug.WriteLine(joke_data);

            //Debug.WriteLine(rep);

        }

        public async Task<string> get_weather()
        {

            Debug.WriteLine("in get_weather");

            Uri resourceUri;

            string strs = "http://api.openweathermap.org/data/2.5/weather?q=jodhpur,in&appid=44db6a862fba0b067b1930da0d769e98";
            // The value of 'AddressField' is set by the user and is therefore untrusted input. If we can't create a
            // valid, absolute URI, we'll notify the user about the incorrect input.
            if (!Helpers.TryGetUri(strs, out resourceUri))
            {
                //rootPage.NotifyUser("Invalid URI.", NotifyType.ErrorMessage);
                return "Incorect Input";
            }

            Helpers.ScenarioStarted(StartButton, CancelButton, OutputField);
            //rootPage.NotifyUser("In progress", NotifyType.StatusMessage);


            HttpResponseMessage response = await httpClient.GetAsync(resourceUri).AsTask(cts.Token);
            isFilterUsed = true;

            await Helpers.DisplayTextResultAsync(response, OutputField, cts.Token);

            string r = await response.Content.ReadAsStringAsync();
            string s = r.ToString();
            //Debug.WriteLine(s);

            string[] seperatedwords = s.Split(',');

            string des = parser(seperatedwords, "description");
            string temp = parser(seperatedwords, "temp");
            string hum = parser(seperatedwords, "humidity");
            string wind  =  parser(seperatedwords, "speed");
            string sunrise = parser(seperatedwords, "sunrise");
            string sunset  = parser(seperatedwords, "sunset");

            Debug.WriteLine(temp
                );

            // WHAT TO DISPLAY
            return temp;



        }


        public string parser(string [] seperatedWords ,string key)
        {

            string intent = "";
            int d = 0;
            int c = 0;
            foreach (string word in seperatedWords)
            {
                c = c + 1;
                if (word == "\""+key+"\":" && d == 0)
                {
                    intent = seperatedWords[c++];
                    string str = "";
                    int i;
                    //Debug.WriteLine(intent.Length);
                    for (i = 1; i < intent.Length; i++)
                    {
                        if (intent[i] == '"')
                            break;

                        //Debug.WriteLine(intent[i]);
                        str = str + intent[i];
                        //Debug.WriteLine(str);                            
                    }
                    //Debug.WriteLine(str);
                    intent = str;
                    Debug.WriteLine(intent);
                    d = 1;
                    break;
                }
                
            }

            return intent;
        }


        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            // Create an instance of SpeechRecognizer.
            speechRecognizer = new SpeechRecognizer(recognizerLanguage);

            // Provide feedback to the user about the state of the recognizer.
            speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

            // Compile the dictation topic constraint, which optimizes for dictated speech.
            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            speechRecognizer.Constraints.Add(dictationConstraint);
            SpeechRecognitionCompilationResult compilationResult = await speechRecognizer.CompileConstraintsAsync();

            // RecognizeWithUIAsync allows developers to customize the prompts.    
            speechRecognizer.UIOptions.AudiblePrompt = "Dictate a phrase or sentence...";
            speechRecognizer.UIOptions.ExampleText = speechResourceMap.GetValue("DictationUIOptionsExampleText", speechContext).ValueAsString;

            // Check to make sure that the constraints were in a proper format and the recognizer was able to compile it.
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                // Disable the recognition buttons.
                btnRecognizeWithUI.IsEnabled = false;
                btnRecognizeWithoutUI.IsEnabled = false;

                // Let the user know that the grammar didn't compile properly.
                resultTextBlock.Visibility = Visibility.Visible;
                resultTextBlock.Text = "Unable to compile grammar.";
            }
        }

        /// <summary>
        /// Handle SpeechRecognizer state changed events by updating a UI component.
        /// </summary>
        /// <param name="sender">Speech recognizer that generated this status event</param>
        /// <param name="args">The recognizer's status</param>
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                MainPage.Current.NotifyUser("Speech recognizer state: " + args.State.ToString(), NotifyType.StatusMessage);
            });
        }

        /// <summary>
        /// Uses the recognizer constructed earlier to listen for speech from the user before displaying 
        /// it back on the screen. Uses the built-in speech recognition UI.
        /// </summary>
        /// <param name="sender">Button that triggered this event</param>
        /// <param name="e">State information about the routed event</param>
        private async void RecognizeWithUIDictationGrammar_Click(object sender, RoutedEventArgs e)
        {
            heardYouSayTextBlock.Visibility = resultTextBlock.Visibility = Visibility.Collapsed;
            hlOpenPrivacySettings.Visibility = Visibility.Collapsed;

            // Start recognition.
            try
            {
                recognitionOperation = speechRecognizer.RecognizeWithUIAsync();
                SpeechRecognitionResult speechRecognitionResult = await recognitionOperation;
                // If successful, display the recognition result.
                if (speechRecognitionResult.Status == SpeechRecognitionResultStatus.Success)
                {
                    heardYouSayTextBlock.Visibility = resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = speechRecognitionResult.Text;
                }
                else
                {
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = string.Format("Speech Recognition Failed, Status: {0}", speechRecognitionResult.Status.ToString());
                }
            }
            catch (TaskCanceledException exception)
            {
                // TaskCanceledException will be thrown if you exit the scenario while the recognizer is actively
                // processing speech. Since this happens here when we navigate out of the scenario, don't try to 
                // show a message dialog for this exception.
                System.Diagnostics.Debug.WriteLine("TaskCanceledException caught while recognition in progress (can be ignored):");
                System.Diagnostics.Debug.WriteLine(exception.ToString());
            }
            catch (Exception exception)
            {
                // Handle the speech privacy policy error.
                if ((uint)exception.HResult == HResultPrivacyStatementDeclined)
                {
                    hlOpenPrivacySettings.Visibility = Visibility.Visible;
                }
                else
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                    await messageDialog.ShowAsync();
                }
            }
        }

        /// <summary>
        /// Uses the recognizer constructed earlier to listen for speech from the user before displaying 
        /// it back on the screen. Uses developer-provided UI for user feedback.
        /// </summary>
        /// <param name="sender">Button that triggered this event</param>
        /// <param name="e">State information about the routed event</param>
            
            private async void RecognizeWithoutUIDictationGrammar_Click(object sender, RoutedEventArgs e)
        {
            heardYouSayTextBlock.Visibility = resultTextBlock.Visibility = Visibility.Collapsed;

            // Disable the UI while recognition is occurring, and provide feedback to the user about current state.
            btnRecognizeWithUI.IsEnabled = false;
            btnRecognizeWithoutUI.IsEnabled = false;
            cbLanguageSelection.IsEnabled = false;
            hlOpenPrivacySettings.Visibility = Visibility.Collapsed;
            //listenWithoutUIButtonText.Text = " listening for speech...";

            // Start recognition.
            try
            {
                recognitionOperation = speechRecognizer.RecognizeAsync();
                SpeechRecognitionResult speechRecognitionResult = await recognitionOperation;

                String str1 = "https://api.projectoxford.ai/luis/v1/application?id=703dfe17-d565-490d-be00-910712691577&subscription-key=f43e1e3b89674540a272f41a042107e5&q=" + speechRecognitionResult.Text;
                responses(str1);
   
                // If successful, display the recognition result.
                if (speechRecognitionResult.Status == SpeechRecognitionResultStatus.Success)
                {
                    heardYouSayTextBlock.Visibility = resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = speechRecognitionResult.Text;
                }
                else
                {
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = string.Format("Speech Recognition Failed, Status: {0}", speechRecognitionResult.Status.ToString());
                }
            }
            catch (TaskCanceledException exception)
            {
                // TaskCanceledException will be thrown if you exit the scenario while the recognizer is actively
                // processing speech. Since this happens here when we navigate out of the scenario, don't try to 
                // show a message dialog for this exception.
                System.Diagnostics.Debug.WriteLine("TaskCanceledException caught while recognition in progress (can be ignored):");
                System.Diagnostics.Debug.WriteLine(exception.ToString());
            }
            catch (Exception exception)
            {
                // Handle the speech privacy policy error.
                if ((uint)exception.HResult == HResultPrivacyStatementDeclined)
                {
                    hlOpenPrivacySettings.Visibility = Visibility.Visible;
                }
                else
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                    await messageDialog.ShowAsync();
                }
            }

            // Reset UI state.
            //listenWithoutUIButtonText.Text = " without UI";
            cbLanguageSelection.IsEnabled = true;
            btnRecognizeWithUI.IsEnabled = true;
            btnRecognizeWithoutUI.IsEnabled = true;
        }

        /// <summary>
        /// Open the Speech, Inking and Typing page under Settings -> Privacy, enabling a user to accept the 
        /// Microsoft Privacy Policy, and enable personalization.
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="args">Ignored</param>
        private async void openPrivacySettings_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-speechtyping"));
        }
    }
}
