﻿#pragma checksum "C:\Users\rishabh\Downloads\Windows-universal-samples-master\Windows-universal-samples-master\Samples\SpeechRecognitionAndSynthesis\cs\PredefinedDictationGrammarScenario.xaml" "{406ea660-64cf-4c82-b6f0-42d48172a799}" "220710E28FCD7EC4302F35CD8B8607F7"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SpeechAndTTS
{
    partial class PredefinedDictationGrammarScenario : 
        global::Windows.UI.Xaml.Controls.Page, 
        global::Windows.UI.Xaml.Markup.IComponentConnector,
        global::Windows.UI.Xaml.Markup.IComponentConnector2
    {
        /// <summary>
        /// Connect()
        /// </summary>
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Windows.UI.Xaml.Build.Tasks"," 14.0.0.0")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public void Connect(int connectionId, object target)
        {
            switch(connectionId)
            {
            case 1:
                {
                    this.RootGrid = (global::Windows.UI.Xaml.Controls.Grid)(target);
                }
                break;
            case 2:
                {
                    this.AddressField = (global::Windows.UI.Xaml.Controls.TextBox)(target);
                }
                break;
            case 3:
                {
                    this.StartButton = (global::Windows.UI.Xaml.Controls.Button)(target);
                    #line 33 "..\..\..\PredefinedDictationGrammarScenario.xaml"
                    ((global::Windows.UI.Xaml.Controls.Button)this.StartButton).Click += this.Start_Click;
                    #line default
                }
                break;
            case 4:
                {
                    this.CancelButton = (global::Windows.UI.Xaml.Controls.Button)(target);
                    #line 34 "..\..\..\PredefinedDictationGrammarScenario.xaml"
                    ((global::Windows.UI.Xaml.Controls.Button)this.CancelButton).Click += this.Cancel_Click;
                    #line default
                }
                break;
            case 5:
                {
                    this.StatusBlock = (global::Windows.UI.Xaml.Controls.TextBlock)(target);
                }
                break;
            case 6:
                {
                    this.heardYouSayTextBlock = (global::Windows.UI.Xaml.Controls.TextBlock)(target);
                }
                break;
            case 7:
                {
                    this.resultTextBlock = (global::Windows.UI.Xaml.Controls.TextBlock)(target);
                }
                break;
            case 8:
                {
                    this.hlOpenPrivacySettings = (global::Windows.UI.Xaml.Controls.TextBlock)(target);
                }
                break;
            case 9:
                {
                    global::Windows.UI.Xaml.Documents.Hyperlink element9 = (global::Windows.UI.Xaml.Documents.Hyperlink)(target);
                    #line 53 "..\..\..\PredefinedDictationGrammarScenario.xaml"
                    ((global::Windows.UI.Xaml.Documents.Hyperlink)element9).Click += this.openPrivacySettings_Click;
                    #line default
                }
                break;
            case 10:
                {
                    this.btnRecognizeWithoutUI = (global::Windows.UI.Xaml.Controls.Button)(target);
                    #line 43 "..\..\..\PredefinedDictationGrammarScenario.xaml"
                    ((global::Windows.UI.Xaml.Controls.Button)this.btnRecognizeWithoutUI).Click += this.RecognizeWithoutUIDictationGrammar_Click;
                    #line default
                }
                break;
            case 11:
                {
                    this.listenWithoutUIButtonText = (global::Windows.UI.Xaml.Controls.TextBlock)(target);
                }
                break;
            case 12:
                {
                    this.cbLanguageSelection = (global::Windows.UI.Xaml.Controls.ComboBox)(target);
                    #line 39 "..\..\..\PredefinedDictationGrammarScenario.xaml"
                    ((global::Windows.UI.Xaml.Controls.ComboBox)this.cbLanguageSelection).SelectionChanged += this.cbLanguageSelection_SelectionChanged;
                    #line default
                }
                break;
            default:
                break;
            }
            this._contentLoaded = true;
        }

        [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Windows.UI.Xaml.Build.Tasks"," 14.0.0.0")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::Windows.UI.Xaml.Markup.IComponentConnector GetBindingConnector(int connectionId, object target)
        {
            global::Windows.UI.Xaml.Markup.IComponentConnector returnValue = null;
            return returnValue;
        }
    }
}
