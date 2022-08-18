using Android.App;
using Android.Content;
using Android.Nfc;
using Android.OS;
using Android.Runtime;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using Plugin.NFC;
using System;
using System.Text;
using System.Threading.Tasks;
using AButton = Android.Widget.Button;
using ATextView = Android.Widget.TextView;

namespace XamAndroidNfcSample
{
    /// <summary>
    /// Example based on Xamarin Forms Sample (Android)
    /// </summary>
    /// <remarks>Be careful of which value is used in your project for the Activity's LaunchMode, by default it's standard but SingleTop or SingleTask seems to work better (<see cref="https://developer.android.com/guide/topics/manifest/activity-element"/>)</remarks>
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)] 
    [IntentFilter(new[] { NfcAdapter.ActionNdefDiscovered }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = MIME_TYPE)]
    public class MainActivity : AppCompatActivity
    {
        public const string MIME_TYPE = "application/com.companyname.xamandroidnfcsample";

        public ATextView Message { get; set; }
        public AButton WriteTagButton { get; set; }
        public AButton ClearTagButton { get; set; }

        NFCNdefTypeFormat _type;
        bool _eventsAlreadySubscribed = false;

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
         
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            
            // Plugin.NFC Initialization
            CrossNFC.Init(this);
            
            SetContentView(Resource.Layout.activity_main);

            Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            WriteTagButton = FindViewById<AButton>(Resource.Id.btnWriteTag);
            WriteTagButton.Click += WriteTagButton_Click;

            ClearTagButton = FindViewById<AButton>(Resource.Id.btnClearTag);
            ClearTagButton.Click += ClearTagButton_Click;

            Message = FindViewById<ATextView>(Resource.Id.tv1);
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            // Plugin NFC: Tag Discovery Interception
            CrossNFC.OnNewIntent(intent);
        }

        protected override async void OnResume()
        {
            base.OnResume();

            // Plugin NFC: Restart NFC listening on resume (needed for Android 10+) 
            CrossNFC.OnResume();

            if (CrossNFC.IsSupported)
            {
                if (!CrossNFC.Current.IsAvailable)
                {
                    ShowAlert("NFC is not available");
                    return;
                }

                if (!CrossNFC.Current.IsEnabled)
                {
                    ShowAlert("NFC is disabled");
                    return;
                }

                SubscribeEvents();

                await BeginListening();
            }
        }

        protected override async void OnDestroy()
        {
            UnsubscribeEvents();
            await StopListening();
            base.OnDestroy();
        }

        /// <summary>
        /// Task to safely start listening for NFC Tags
        /// </summary>
        /// <returns>The task to be performed</returns>
        Task BeginListening()
        {
            try
            {
                CrossNFC.Current.StartListening();
            }
            catch (Exception ex)
            {
                ShowAlert(ex.Message);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Task to safely stop listening for NFC tags
        /// </summary>
        /// <returns>The task to be performed</returns>
        Task StopListening()
        {
            try
            {
                CrossNFC.Current.StopListening();
            }
            catch (Exception ex)
            {
                ShowAlert(ex.Message);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscribe to the NFC events
        /// </summary>
        void SubscribeEvents()
        {
            if (_eventsAlreadySubscribed)
                return;

            _eventsAlreadySubscribed = true;

            CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
            CrossNFC.Current.OnMessagePublished += Current_OnMessagePublished;
            CrossNFC.Current.OnTagDiscovered += Current_OnTagDiscovered;
            CrossNFC.Current.OnNfcStatusChanged += Current_OnNfcStatusChanged;
            CrossNFC.Current.OnTagListeningStatusChanged += Current_OnTagListeningStatusChanged;
        }

        /// <summary>
        /// Unsubscribe from the NFC events
        /// </summary>
        void UnsubscribeEvents()
        {
            CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
            CrossNFC.Current.OnMessagePublished -= Current_OnMessagePublished;
            CrossNFC.Current.OnTagDiscovered -= Current_OnTagDiscovered;
            CrossNFC.Current.OnNfcStatusChanged -= Current_OnNfcStatusChanged;
            CrossNFC.Current.OnTagListeningStatusChanged -= Current_OnTagListeningStatusChanged;
        }

        /// <summary>
        /// Write Tag button click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void WriteTagButton_Click(object sender, EventArgs e) => await Publish(NFCNdefTypeFormat.WellKnown);

        /// <summary>
        /// Clear Tag button click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ClearTagButton_Click(object sender, EventArgs e) => await Publish();

        /// <summary>
        /// Event raised when NFC listener status changed
        /// </summary>
        /// <param name="isListening"></param>
        private void Current_OnTagListeningStatusChanged(bool isListening)
        {
            var message = $"NFC Listener is {(isListening ? "ON" : "OFF")}";
            ShowDebug(message);
            Message.Text = message;
        }

        /// <summary>
        /// Event raised when NFC status changed
        /// </summary>
        /// <param name="isEnabled"></param>
        private void Current_OnNfcStatusChanged(bool isEnabled) => ShowAlert($"NFC has been {(isEnabled ? "enabled" : "disabled")}", debug: true);

        /// <summary>
        /// Event raised when a tag is discovered
        /// </summary>
        /// <param name="tagInfo"></param>
        /// <param name="format"></param>
        private void Current_OnTagDiscovered(ITagInfo tagInfo, bool format)
        {
            ShowDebug("OnTagDiscovered fired!");

            if (!CrossNFC.Current.IsWritingTagSupported)
            {
                ShowAlert("Writing tag is not supported on this device");
                return;
            }

            try
            {
                NFCNdefRecord record = null;
                switch (_type)
                {
                    case NFCNdefTypeFormat.WellKnown:
                        record = new NFCNdefRecord
                        {
                            TypeFormat = NFCNdefTypeFormat.WellKnown,
                            MimeType = MIME_TYPE,
                            Payload = NFCUtils.EncodeToByteArray("Plugin.NFC is awesome!"),
                            LanguageCode = "en"
                        };
                        break;
                    case NFCNdefTypeFormat.Uri:
                        record = new NFCNdefRecord
                        {
                            TypeFormat = NFCNdefTypeFormat.Uri,
                            Payload = NFCUtils.EncodeToByteArray("https://github.com/franckbour/Plugin.NFC")
                        };
                        break;
                    case NFCNdefTypeFormat.Mime:
                        record = new NFCNdefRecord
                        {
                            TypeFormat = NFCNdefTypeFormat.Mime,
                            MimeType = MIME_TYPE,
                            Payload = NFCUtils.EncodeToByteArray("Plugin.NFC is awesome!")
                        };
                        break;
                    default:
                        break;
                }

                if (!format && record == null)
                    throw new Exception("Record can't be null.");

                tagInfo.Records = new[] { record };

                if (format)
                    CrossNFC.Current.ClearMessage(tagInfo);
                else
                {
                    CrossNFC.Current.PublishMessage(tagInfo, false);
                }
            }
            catch (Exception ex)
            {
                ShowAlert(ex.Message);
            }
        }

        /// <summary>
        /// Event raised when a message is published
        /// </summary>
        /// <param name="tagInfo"></param>
        private void Current_OnMessagePublished(ITagInfo tagInfo)
        {
            ShowDebug("OnMessagePublished fired!");

            try
            {
                CrossNFC.Current.StopPublishing();
                if (tagInfo.IsEmpty)
                    ShowAlert("Formatting tag operation successful");
                else
                    ShowAlert("Writing tag operation successful");
            }
            catch (Exception ex)
            {
                ShowAlert(ex.Message);
            }
        }

        /// <summary>
        /// Event raised when a message is received
        /// </summary>
        /// <param name="tagInfo"></param>
        private void Current_OnMessageReceived(ITagInfo tagInfo)
        {
            if (tagInfo == null)
            {
                ShowAlert("No tag found");
                return;
            }

            // Customized serial number
            var identifier = tagInfo.Identifier;
            var serialNumber = NFCUtils.ByteArrayToHexString(identifier, ":");
            var title = !string.IsNullOrWhiteSpace(serialNumber) ? $"Tag [{serialNumber}]" : "Tag Info";

            if (!tagInfo.IsSupported)
            {
                ShowAlert("Unsupported tag (app)", title);
            }
            else if (tagInfo.IsEmpty)
            {
                ShowAlert("Empty tag", title);
            }
            else
            {
                var first = tagInfo.Records[0];
                ShowAlert(GetMessage(first), title);
            }
        }

        /// <summary>
		/// Returns the tag information from NDEF record
		/// </summary>
		/// <param name="record"><see cref="NFCNdefRecord"/></param>
		/// <returns>The tag information</returns>
		string GetMessage(NFCNdefRecord record)
        {
            var message = $"Message: {record.Message}";
            message += System.Environment.NewLine;
            message += $"RawMessage: {Encoding.UTF8.GetString(record.Payload)}";
            message += System.Environment.NewLine;
            message += $"Type: {record.TypeFormat}";

            if (!string.IsNullOrWhiteSpace(record.MimeType))
            {
                message += System.Environment.NewLine;
                message += $"MimeType: {record.MimeType}";
            }

            return message;
        }

        /// <summary>
        /// Task to publish data to the tag
        /// </summary>
        /// <param name="type"><see cref="NFCNdefTypeFormat"/></param>
        /// <returns>The task to be performed</returns>
        async Task Publish(NFCNdefTypeFormat? type = null)
        {
            await BeginListening();
            try
            {
                _type = NFCNdefTypeFormat.Empty;
                if (type.HasValue) _type = type.Value;
                CrossNFC.Current.StartPublishing(!type.HasValue);
            }
            catch (Exception ex)
            {
                ShowAlert(ex.Message);
            }
        }

        /// <summary>
        /// Print debug message in output
        /// </summary>
        /// <param name="message"></param>
        private void ShowDebug(string message) => System.Diagnostics.Debug.WriteLine(message);

        /// <summary>
        /// Show Alert Message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="debug"></param>
        private void ShowAlert(string message, string title = null, bool debug = false)
        {
            if (debug) 
                ShowDebug(message);

            new Android.App.AlertDialog.Builder(this)
                .SetTitle(title ?? "Plugin NFC")
                .SetMessage(message)
                .SetPositiveButton("OK", (s, e) => { })
                .Show();
        }
	}
}
