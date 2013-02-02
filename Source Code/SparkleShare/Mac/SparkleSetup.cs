//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
using MonoMac.WebKit;

using Mono.Unix;

namespace SparkleShare {

    public class SparkleSetup : SparkleSetupWindow {

        public SparkleSetupController Controller = new SparkleSetupController ();

        private NSButton ContinueButton;
        private NSButton AddButton;
        private NSButton TryAgainButton;
        private NSButton CancelButton;
        private NSButton SkipTutorialButton;
        private NSButton StartupCheckButton;
        private NSButton HistoryCheckButton;
        private NSButton ShowPasswordCheckButton;
        private NSButton OpenFolderButton;
        private NSButton FinishButton;
        private NSImage SlideImage;
        private NSImageView SlideImageView;
        private NSProgressIndicator ProgressIndicator;
        private NSTextField EmailLabel;
        private NSTextField EmailTextField;
        private NSTextField EmailHelpLabel;
        private NSTextField FullNameTextField;
        private NSTextField FullNameLabel;
        private NSTextField AddressTextField;
        private NSTextField AddressLabel;
        private NSTextField AddressHelpLabel;
        private NSTextField PathTextField;
        private NSTextField PathLabel;
        private NSTextField PathHelpLabel;
        private NSTextField PasswordTextField;
        private NSTextField VisiblePasswordTextField;
        private NSTextField PasswordLabel;
        private NSTextField WarningTextField;
        private NSImage WarningImage;
        private NSImageView WarningImageView;
        private NSTableView TableView;
        private NSScrollView ScrollView;
        private NSTableColumn IconColumn;
        private NSTableColumn DescriptionColumn;
        private SparkleDataSource DataSource;
 

        public SparkleSetup () : base ()
        {
            Controller.HideWindowEvent += delegate {
                InvokeOnMainThread (delegate {
                    PerformClose (this);
                });
            };

            Controller.ShowWindowEvent += delegate {
                InvokeOnMainThread (delegate {
                    OrderFrontRegardless ();
                });
            };

            Controller.ChangePageEvent += delegate (PageType type, string [] warnings) {
                using (var a = new NSAutoreleasePool ())
                {
                    InvokeOnMainThread (delegate {
                        Reset ();
                        ShowPage (type, warnings);
                        ShowAll ();
                    });
                }
            };
        }


        public void ShowPage (PageType type, string [] warnings)
        {
            if (type == PageType.Setup) {
                Header      = "Welcome to QloudSync!";


                FullNameLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (165, Frame.Height - 234, 160, 17),
                    StringValue     = "Username:",
                    Font            = SparkleUI.Font
                };

                FullNameTextField = new NSTextField () {
                    Frame       = new RectangleF (330, Frame.Height - 238, 196, 22),
                    //StringValue = UnixUserInfo.GetRealUser ().RealName,
                    Delegate    = new SparkleTextFieldDelegate ()
                };

                EmailLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (165, Frame.Height - 264, 160, 17),
                    StringValue     = "Password:",
                    Font            = SparkleUI.Font
                };

                NSSecureTextField PasswordTextField = new NSSecureTextField(){
                    Frame       = new RectangleF (330, Frame.Height - 268, 196, 22),
                    Delegate    = new SparkleTextFieldDelegate (),
                };


                NSTextField MessageLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Left,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (325, 75 , Frame.Width, 60),
                    StringValue     = "The password you entered is incorrect.\n",
                    Font            = NSFontManager.SharedFontManager.FontWithFamily (
                        "Lucida Grande", NSFontTraitMask.Bold, 0, 10)
                };


                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                ContinueButton = new NSButton () {
                    Title    = "Continue",
                    Enabled  = false
                };

                (PasswordTextField.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    if(PasswordTextField.StringValue.Length < 3)
                        ContinueButton.Enabled = false;
                    else
                        ContinueButton.Enabled = true;
                };

                CancelButton.Activated += delegate {
                    Controller.PageCancelled ();
                };

                ContinueButton.Activated += delegate {
                    try{
                        SQ.Net.Authentication aut = new SQ.Net.Authentication();
                        aut.Authenticate (FullNameTextField.StringValue, PasswordTextField.StringValue);
                        SQ.QloudSyncPlugin.WriteKeys();
                        Controller.AddPageCompleted (FullNameTextField.StringValue, PasswordTextField.StringValue);
                    }
                    catch (System.Net.WebException)
                    {
                        ContentView.AddSubview (MessageLabel);
                    }
                };

                ContentView.AddSubview (FullNameLabel);
                ContentView.AddSubview (FullNameTextField);
                ContentView.AddSubview (EmailLabel);
                ContentView.AddSubview (PasswordTextField);


                Buttons.Add (ContinueButton);
                Buttons.Add (CancelButton);

                //Controller.CheckSetupPage (FullNameTextField.StringValue, EmailTextField.StringValue);
            }

            if (type == PageType.Invite) {
                Header      = "You've received an invite!";
                Description = "Do you want to add this project to QloudSync?";


                AddressLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (165, Frame.Height - 240, 160, 17),
                    StringValue     = "Address:",
                    Font            = SparkleUI.Font
                };

                PathLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (165, Frame.Height - 264, 160, 17),
                    StringValue     = "Remote Path:",
                    Font            = SparkleUI.Font
                };

                AddressTextField = new NSTextField () {
                    Alignment       = NSTextAlignment.Left,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (330, Frame.Height - 240, 260, 17),
                    StringValue     = Controller.PendingInvite.Address,
                    Font            = SparkleUI.BoldFont
                };

                PathTextField = new NSTextField () {
                    Alignment       = NSTextAlignment.Left,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (330, Frame.Height - 264, 260, 17),
                    StringValue     = Controller.PendingInvite.RemotePath,
                    Font            = SparkleUI.BoldFont
                };

                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                AddButton = new NSButton () {
                    Title = "Add"
                };


                CancelButton.Activated += delegate {
                    Controller.PageCancelled ();
                };

                AddButton.Activated += delegate {
                    Controller.InvitePageCompleted ();
                };


                ContentView.AddSubview (AddressLabel);
                ContentView.AddSubview (PathLabel);
                ContentView.AddSubview (AddressTextField);
                ContentView.AddSubview (PathTextField);

                Buttons.Add (AddButton);
                Buttons.Add (CancelButton);
            }

            if (type == PageType.Add) {
                Header      = "Where's your project hosted?";
                Description = "";


                AddressLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Left,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (190, Frame.Height - 308, 160, 17),
                    StringValue     = "Address:",
                    Font            = SparkleUI.BoldFont
                };

                AddressTextField = new NSTextField () {
                    Frame       = new RectangleF (190, Frame.Height - 336, 196, 22),
                    Font        = SparkleUI.Font,
                    Enabled     = (Controller.SelectedPlugin.Address == null),
                    Delegate    = new SparkleTextFieldDelegate (),
                    StringValue = "" + Controller.PreviousAddress
                };

                AddressTextField.Cell.LineBreakMode = NSLineBreakMode.TruncatingTail;

                PathLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Left,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (190 + 196 + 16, Frame.Height - 308, 160, 17),
                    StringValue     = "Remote Path:",
                    Font            = SparkleUI.BoldFont
                };

                PathTextField = new NSTextField () {
                    Frame       = new RectangleF (190 + 196 + 16, Frame.Height - 336, 196, 22),
                    Enabled     = (Controller.SelectedPlugin.Path == null),
                    Delegate    = new SparkleTextFieldDelegate (),
                    StringValue = "" + Controller.PreviousPath
                };

                PathTextField.Cell.LineBreakMode = NSLineBreakMode.TruncatingTail;

                PathHelpLabel = new NSTextField () {
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    TextColor       = NSColor.DisabledControlText,
                    Editable        = false,
                    Frame           = new RectangleF (190 + 196 + 16, Frame.Height - 355, 204, 17),
                    Font            = NSFontManager.SharedFontManager.FontWithFamily ("Lucida Grande",
                        NSFontTraitMask.Condensed, 0, 11),
                    StringValue = "" //+ Controller.SelectedPlugin.PathExample
                };

                AddressHelpLabel = new NSTextField () {
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    TextColor       = NSColor.DisabledControlText,
                    Editable        = false,
                    Frame           = new RectangleF (190, Frame.Height - 355, 204, 17),
                    Font            = NSFontManager.SharedFontManager.FontWithFamily ("Lucida Grande",
                        NSFontTraitMask.Condensed, 0, 11),
                    StringValue = "" + Controller.SelectedPlugin.AddressExample
                };

                TableView = new NSTableView () {
                    Frame            = new RectangleF (0, 0, 0, 0),
                    RowHeight        = 34,
                    IntercellSpacing = new SizeF (8, 12),
                    HeaderView       = null,
                    Delegate         = new SparkleTableViewDelegate ()
                };

                ScrollView = new NSScrollView () {
                    Frame               = new RectangleF (190, Frame.Height - 280, 408, 185),
                    DocumentView        = TableView,
                    HasVerticalScroller = true,
                    BorderType          = NSBorderType.BezelBorder
                };

                IconColumn = new NSTableColumn (new NSImage ()) {
                    Width = 36,
                    HeaderToolTip = "Icon",
                    DataCell = new NSImageCell () {
                        ImageAlignment = NSImageAlignment.Right
                    }
                };

                DescriptionColumn = new NSTableColumn () {
                    Width         = 350,
                    HeaderToolTip = "Description",
                    Editable      = false
                };

                DescriptionColumn.DataCell.Font = NSFontManager.SharedFontManager.FontWithFamily ("Lucida Grande",
                    NSFontTraitMask.Condensed, 0, 11);

                TableView.AddColumn (IconColumn);
                TableView.AddColumn (DescriptionColumn);

                DataSource = new SparkleDataSource (Controller.Plugins);

                TableView.DataSource = DataSource;
                TableView.ReloadData ();

                HistoryCheckButton = new NSButton () {
                    Frame = new RectangleF (190, Frame.Height - 400, 300, 18),
                    Title = "Fetch prior revisions"
                };

                if (Controller.FetchPriorHistory)
                    HistoryCheckButton.State = NSCellStateValue.On;

                HistoryCheckButton.SetButtonType (NSButtonType.Switch);

                AddButton = new NSButton () {
                    Title = "Add",
                    Enabled = false
                };

                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                NSSecureTextField Password = new NSSecureTextField(){
                    Frame       = new RectangleF (190 + 196 + 16, Frame.Height - 336, 196, 22),
                    Enabled     = (Controller.SelectedPlugin.Path == null),
                    Delegate    = new SparkleTextFieldDelegate (),
                    Hidden = true
                };

                ContentView.AddSubview (Password);

                //adding the plugin support to the program

                Controller.ChangeAddressFieldEvent += delegate(string text, 
                    string example_text, FieldState state) {

                    if (text == "QloudSync" && example_text == "QloudSync"){
                        InvokeOnMainThread (delegate {

                            AddressLabel.StringValue = "Login:";
                            AddressTextField.Enabled = true;
                            AddressTextField.StringValue = "";
                            PathLabel.StringValue = "Password:";
                            PathTextField.StringValue = "";
                            PathTextField.Enabled = true;
                            PathHelpLabel.StringValue = "";
                            AddressHelpLabel.StringValue = "";
                            Password.Hidden = false;
                            PathTextField.Hidden = true;
                            PathTextField.Enabled = false;
                        });
                   }
                   else
                   {
                        InvokeOnMainThread (delegate {
                           
                            AddressTextField.StringValue = text;
                            AddressTextField.Enabled     = (state == FieldState.Enabled);
                            AddressHelpLabel.StringValue = example_text;
                            Password.StringValue = "";
                            Password.Hidden = true;
                            PathTextField.Hidden = false;
                            //PathTextField.StringValue = "";
                            AddressLabel.StringValue = "Address:";
                            PathLabel.StringValue = "Remote Path:";
                        });
                   }
                };

                Controller.ChangePathFieldEvent += delegate (string text,
                    string example_text, FieldState state) {

                    InvokeOnMainThread (delegate {
                        //PathTextField.StringValue = text;
                        PathTextField.Enabled     = (state == FieldState.Enabled);
                        PathHelpLabel.StringValue = example_text;
                    });
                };


               

                TableView.SelectRow (Controller.SelectedPluginIndex, false);
                TableView.ScrollRowToVisible (Controller.SelectedPluginIndex);

                (AddressTextField.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    Controller.CheckAddPage (AddressTextField.StringValue, PathTextField.StringValue, TableView.SelectedRow);
                };

                 (PathTextField.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    Controller.CheckAddPage (AddressTextField.StringValue, PathTextField.StringValue, TableView.SelectedRow);
                };

                (Password.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    Controller.CheckAddPage (AddressTextField.StringValue, Password.StringValue, TableView.SelectedRow);
                };

                (TableView.Delegate as SparkleTableViewDelegate).SelectionChanged += delegate {
                    Controller.SelectedPluginChanged (TableView.SelectedRow);
                    Controller.CheckAddPage (AddressTextField.StringValue, PathTextField.StringValue, TableView.SelectedRow);
                };

                HistoryCheckButton.Activated += delegate {
                    Controller.HistoryItemChanged (HistoryCheckButton.State == NSCellStateValue.On);
                };

                AddButton.Activated += delegate {
                    if (Password.StringValue == "")
                        Controller.AddPageCompleted (AddressTextField.StringValue, PathTextField.StringValue);
                    else
                        Controller.AddPageCompleted (AddressTextField.StringValue, Password.StringValue);
                };

                CancelButton.Activated += delegate {
                    Controller.PageCancelled ();
                };

                Controller.UpdateAddProjectButtonEvent += delegate (bool button_enabled) {
                    InvokeOnMainThread (delegate {
                        AddButton.Enabled = button_enabled;
                    });
                };


                ContentView.AddSubview (ScrollView);
                ContentView.AddSubview (AddressLabel);
                ContentView.AddSubview (AddressTextField);
                ContentView.AddSubview (AddressHelpLabel);
                ContentView.AddSubview (PathLabel);
                ContentView.AddSubview (PathTextField);
                ContentView.AddSubview (PathHelpLabel);
                ContentView.AddSubview (HistoryCheckButton);

                Buttons.Add (AddButton);
                Buttons.Add (CancelButton);

                Controller.CheckAddPage (AddressTextField.StringValue, PathTextField.StringValue, TableView.SelectedRow);
            }

            if (type == PageType.Syncing) {
                Header      = "Adding project ‘" + Controller.SyncingFolder + "’…";
                Description = "This may take a while for large projects.\nIsn't it coffee-o'clock?";


                ProgressIndicator = new NSProgressIndicator () {
                    Frame         = new RectangleF (190, Frame.Height - 200, 640 - 150 - 80, 20),
                    Style         = NSProgressIndicatorStyle.Bar,
                    MinValue      = 0.0,
                    MaxValue      = 100.0,
                    Indeterminate = false,
                    DoubleValue   = Controller.ProgressBarPercentage
                };

                ProgressIndicator.StartAnimation (this);

                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                FinishButton = new NSButton () {
                    Title = "Finish",
                    Enabled = false
                };


                Controller.UpdateProgressBarEvent += delegate (double percentage) {
                    InvokeOnMainThread (() => {
                        ProgressIndicator.DoubleValue = percentage;
                    });
                };

                CancelButton.Activated += delegate {

                    Controller.SyncingCancelled ();

                };


                ContentView.AddSubview (ProgressIndicator);

                Buttons.Add (FinishButton);
                Buttons.Add (CancelButton);
            }

            if (type == PageType.Error) {
                Header      = "Oops! Something went wrong…";
                Description = "Please check the following:";


                // Displaying marked up text with Cocoa is
                // a pain, so we just use a webview instead
                WebView web_view = new WebView ();
                web_view.Frame   = new RectangleF (190, Frame.Height - 525, 375, 400);

                string html = "<style>" +
                    "* {" +
                    "  font-family: 'Lucida Grande';" +
                    "  font-size: 12px; cursor: default;" +
                    "}" +
                    "body {" +
                    "  -webkit-user-select: none;" +
                    "  margin: 0;" +
                    "  padding: 3px;" +
                    "}" +
                    "li {" +
                    "  margin-bottom: 16px;" +
                    "  margin-left: 0;" +
                    "  padding-left: 0;" +
                    "  line-height: 20px;" +
                    "}" +
                    "ul {" +
                    "  padding-left: 24px;" +
                    "}" +
                    "</style>" +
                    "<ul>" +
                    "  <li><b>" + Controller.PreviousUrl + "</b> is the address we've compiled. Does this look alright?</li>" +
                    "  <li>Do you have access rights to this remote project?</li>" +
                    "</ul>";

                if (warnings.Length > 0) {
                    string warnings_markup = "";

                    foreach (string warning in warnings)
                        warnings_markup += "<br><b>" + warning + "</b>";

                    html = html.Replace ("</ul>", "<li>Here's the raw error message: " + warnings_markup + "</li></ul>");
                }

                web_view.MainFrame.LoadHtmlString (html, new NSUrl (""));
                web_view.DrawsBackground = false;

                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                TryAgainButton = new NSButton () {
                    Title = "Try again…"
                };


                CancelButton.Activated += delegate {
                    Controller.PageCancelled ();
                };

                TryAgainButton.Activated += delegate {
                    Controller.ErrorPageCompleted ();
                };


                ContentView.AddSubview (web_view);

                Buttons.Add (TryAgainButton);
                Buttons.Add (CancelButton);
            }

            if (type == PageType.CryptoSetup) {
                Header      = "Set up file encryption";
                Description = "This project is supposed to be encrypted, but it doesn't yet have a password set. Please provide one below:";


                PasswordLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (155, Frame.Height - 204, 160, 17),
                    StringValue     = "Password:",
                    Font            = SparkleUI.BoldFont
                };

                PasswordTextField = new NSSecureTextField () {
                    Frame       = new RectangleF (320, Frame.Height - 208, 196, 22),
                    Delegate    = new SparkleTextFieldDelegate ()
                };

                VisiblePasswordTextField = new NSTextField () {
                    Frame       = new RectangleF (320, Frame.Height - 208, 196, 22),
                    Delegate    = new SparkleTextFieldDelegate ()
                };

                ShowPasswordCheckButton = new NSButton () {
                    Frame = new RectangleF (318, Frame.Height - 235, 300, 18),
                    Title = "Show password",
                    State = NSCellStateValue.Off
                };

                ShowPasswordCheckButton.SetButtonType (NSButtonType.Switch);

                WarningImage = NSImage.ImageNamed ("NSInfo");
                WarningImage.Size = new SizeF (24, 24);

                WarningImageView = new NSImageView () {
                    Image = WarningImage,
                    Frame = new RectangleF (200, Frame.Height - 320, 24, 24)
                };

                WarningTextField = new NSTextField () {
                    Frame           = new RectangleF (235, Frame.Height - 390, 325, 100),
                    StringValue     = "This password can't be changed later, and your files can't be recovered if it's forgotten.",
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Font            = SparkleUI.Font
                };

                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                ContinueButton = new NSButton () {
                    Title    = "Continue",
                    Enabled  = false
                };


                ShowPasswordCheckButton.Activated += delegate {
                    if (PasswordTextField.Superview == ContentView) {
                        PasswordTextField.RemoveFromSuperview ();
                        ContentView.AddSubview (VisiblePasswordTextField);

                    } else {
                        VisiblePasswordTextField.RemoveFromSuperview ();
                        ContentView.AddSubview (PasswordTextField);
                    }
                };

                (PasswordTextField.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    VisiblePasswordTextField.StringValue = PasswordTextField.StringValue;
                    Controller.CheckCryptoSetupPage (PasswordTextField.StringValue);
                };

                (VisiblePasswordTextField.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    PasswordTextField.StringValue = VisiblePasswordTextField.StringValue;
                    Controller.CheckCryptoSetupPage (PasswordTextField.StringValue);
                };


                Controller.UpdateCryptoSetupContinueButtonEvent += delegate (bool button_enabled) {
                    InvokeOnMainThread (() => {
                        ContinueButton.Enabled = button_enabled;
                    });
                };

                ContinueButton.Activated += delegate {
                   Controller.CryptoSetupPageCompleted (PasswordTextField.StringValue);
                };

                CancelButton.Activated += delegate {
                    Controller.CryptoPageCancelled ();
                };


                ContentView.AddSubview (PasswordLabel);
                ContentView.AddSubview (PasswordTextField);
                ContentView.AddSubview (ShowPasswordCheckButton);
                ContentView.AddSubview (WarningImageView);
                ContentView.AddSubview (WarningTextField);

                Buttons.Add (ContinueButton);
                Buttons.Add (CancelButton);

                NSApplication.SharedApplication.RequestUserAttention (NSRequestUserAttentionType.CriticalRequest);
            }

            if (type == PageType.CryptoPassword) {
                Header      = "This project contains encrypted files";
                Description = "Please enter the password to see their contents.";

                PasswordLabel = new NSTextField () {
                    Alignment       = NSTextAlignment.Right,
                    BackgroundColor = NSColor.WindowBackground,
                    Bordered        = false,
                    Editable        = false,
                    Frame           = new RectangleF (155, Frame.Height - 224, 160, 17),
                    StringValue     = "Password:",
                    Font            = SparkleUI.BoldFont
                };

                PasswordTextField = new NSSecureTextField () {
                    Frame       = new RectangleF (320, Frame.Height - 228, 196, 22),
                    Delegate    = new SparkleTextFieldDelegate ()
                };

                VisiblePasswordTextField = new NSTextField () {
                    Frame       = new RectangleF (320, Frame.Height - 228, 196, 22),
                    Delegate    = new SparkleTextFieldDelegate ()
                };

                ShowPasswordCheckButton = new NSButton () {
                    Frame = new RectangleF (318, Frame.Height - 255, 300, 18),
                    Title = "Show password",
                    State = NSCellStateValue.Off
                };

                ShowPasswordCheckButton.SetButtonType (NSButtonType.Switch);

                CancelButton = new NSButton () {
                    Title = "Cancel"
                };

                ContinueButton = new NSButton () {
                    Title    = "Continue",
                    Enabled  = false
                };


                Controller.UpdateCryptoPasswordContinueButtonEvent += delegate (bool button_enabled) {
                    InvokeOnMainThread (() => {
                        ContinueButton.Enabled = button_enabled;
                    });
                };

                ShowPasswordCheckButton.Activated += delegate {
                    if (PasswordTextField.Superview == ContentView) {
                        PasswordTextField.RemoveFromSuperview ();
                        ContentView.AddSubview (VisiblePasswordTextField);

                    } else {
                        VisiblePasswordTextField.RemoveFromSuperview ();
                        ContentView.AddSubview (PasswordTextField);
                    }
                };

                (PasswordTextField.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    VisiblePasswordTextField.StringValue = PasswordTextField.StringValue;
                    Controller.CheckCryptoPasswordPage (PasswordTextField.StringValue);
                };

                (VisiblePasswordTextField.Delegate as SparkleTextFieldDelegate).StringValueChanged += delegate {
                    PasswordTextField.StringValue = VisiblePasswordTextField.StringValue;
                    Controller.CheckCryptoPasswordPage (PasswordTextField.StringValue);
                };

                ContinueButton.Activated += delegate {
                   Controller.CryptoPasswordPageCompleted (PasswordTextField.StringValue);
                };

                CancelButton.Activated += delegate {
                    Controller.CryptoPageCancelled ();
                };


                ContentView.AddSubview (PasswordLabel);
                ContentView.AddSubview (PasswordTextField);
                ContentView.AddSubview (ShowPasswordCheckButton);

                Buttons.Add (ContinueButton);
                Buttons.Add (CancelButton);

                NSApplication.SharedApplication.RequestUserAttention (NSRequestUserAttentionType.CriticalRequest);
            }


            if (type == PageType.Finished) {
                Header      = "Your shared project is ready!";
                Description = "You can find the files in your QloudSync folder.";


                if (warnings.Length > 0) {
                    WarningImage = NSImage.ImageNamed ("NSInfo");
                    WarningImage.Size = new SizeF (24, 24);

                    WarningImageView = new NSImageView () {
                        Image = WarningImage,
                        Frame = new RectangleF (200, Frame.Height - 175, 24, 24)
                    };

                    WarningTextField = new NSTextField () {
                        Frame           = new RectangleF (235, Frame.Height - 245, 325, 100),
                        StringValue     = warnings [0],
                        BackgroundColor = NSColor.WindowBackground,
                        Bordered        = false,
                        Editable        = false,
                        Font            = SparkleUI.Font
                    };

                    ContentView.AddSubview (WarningImageView);
                    ContentView.AddSubview (WarningTextField);
                }


                OpenFolderButton = new NSButton () {
                    Title = "Show folder"
                };

                FinishButton = new NSButton () {
                    Title = "Finish"
                };


                OpenFolderButton.Activated += delegate {
                    Controller.OpenFolderClicked ();
                };

                FinishButton.Activated += delegate {
                    SQ.QloudSyncPlugin.InitRepo = true;
                    Controller.FinishPageCompleted ();
                };


                Buttons.Add (FinishButton);
                Buttons.Add (OpenFolderButton);

                NSApplication.SharedApplication.RequestUserAttention (NSRequestUserAttentionType.CriticalRequest);
            }

            if (type == PageType.Tutorial) {
                string slide_image_path = Path.Combine (NSBundle.MainBundle.ResourcePath,
                    "Pixmaps", "tutorial-slide-" + Controller.TutorialPageNumber + ".png");

                SlideImage = new NSImage (slide_image_path) {
                    Size = new SizeF (350, 200)
                };

                SlideImageView = new NSImageView () {
                    Image = SlideImage,
                    Frame = new RectangleF (215, Frame.Height - 350, 350, 200)
                };

                ContentView.AddSubview (SlideImageView);


                switch (Controller.TutorialPageNumber) {

                    case 1: {
                        Header      = "What's happening next?";
                        Description = "QloudSync creates a special folder on your computer " +
                            "that will keep track of your projects.";


                        SkipTutorialButton = new NSButton () {
                            Title = "Skip Tutorial"
                        };

                        ContinueButton = new NSButton () {
                            Title = "Continue"
                        };


                        SkipTutorialButton.Activated += delegate {
                            Controller.TutorialSkipped ();
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };


                        ContentView.AddSubview (SlideImageView);

                        Buttons.Add (ContinueButton);
                        Buttons.Add (SkipTutorialButton);

                        break;
                    }

                    case 2: {
                        Header      = "Sharing files with others";
                        Description = "All files added to your project folders are synced automatically with " +
                            "the host and your team members.";

                        ContinueButton = new NSButton () {
                            Title = "Continue"
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Buttons.Add (ContinueButton);

                        break;
                    }

                    case 3: {
                        Header      = "The status icon is here to help";
                        Description = "It shows the syncing progress, provides easy access to " +
                            "your projects and let's you view recent changes.";

                        ContinueButton = new NSButton () {
                            Title = "Continue"
                        };

                        ContinueButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };

                        Buttons.Add (ContinueButton);

                        break;
                    }

                    case 4: {
                        Header      = "Adding projects to QloudSync";
                        Description = "You can do this through the status icon menu, or by clicking " +
                            "magic buttons on webpages that look like this:";


                        StartupCheckButton = new NSButton () {
                            Frame = new RectangleF (190, Frame.Height - 400, 300, 18),
                            Title = "Add QloudSync to startup items",
                            State = NSCellStateValue.On
                        };

                        StartupCheckButton.SetButtonType (NSButtonType.Switch);

                        FinishButton = new NSButton () {
                            Title = "Finish"
                        };

                        SlideImage.Size = new SizeF (350, 64);


                        StartupCheckButton.Activated += delegate {
                            Controller.StartupItemChanged (StartupCheckButton.State == NSCellStateValue.On);
                        };

                        FinishButton.Activated += delegate {
                            Controller.TutorialPageCompleted ();
                        };


                        ContentView.AddSubview (StartupCheckButton);
                        Buttons.Add (FinishButton);

                        break;
                    }
                }
            }
        }
    }


    [Register("SparkleDataSource")]
    public class SparkleDataSource : NSTableViewDataSource {

        public List<object> Items ;
        public NSAttributedString [] Cells;
        public NSAttributedString [] SelectedCells;


        public SparkleDataSource (List<SparklePlugin> plugins)
        {
            Items         = new List <object> ();
            Cells         = new NSAttributedString [plugins.Count];
            SelectedCells = new NSAttributedString [plugins.Count];

            int i = 0;
            foreach (SparklePlugin plugin in plugins) {
                Items.Add (plugin);

                NSTextFieldCell cell = new NSTextFieldCell ();

                NSData name_data = NSData.FromString ("<font face='Lucida Grande'><b>" + plugin.Name + "</b></font>");

                NSDictionary name_dictionary       = new NSDictionary();
                NSAttributedString name_attributes = new NSAttributedString (
                    name_data, new NSUrl ("file://"), out name_dictionary);

                NSData description_data = NSData.FromString (
                    "<small><font style='line-height: 150%' color='#aaa' face='Lucida Grande'>" + plugin.Description + "</font></small>");

                NSDictionary description_dictionary       = new NSDictionary();
                NSAttributedString description_attributes = new NSAttributedString (
                    description_data, new NSUrl ("file://"), out description_dictionary);

                NSMutableAttributedString mutable_attributes = new NSMutableAttributedString (name_attributes);
                mutable_attributes.Append (new NSAttributedString ("\n"));
                mutable_attributes.Append (description_attributes);

                cell.AttributedStringValue = mutable_attributes;
                Cells [i] = (NSAttributedString) cell.ObjectValue;

                NSTextFieldCell selected_cell = new NSTextFieldCell ();

                NSData selected_name_data = NSData.FromString (
                    "<font color='white' face='Lucida Grande'><b>" + plugin.Name + "</b></font>");

                NSDictionary selected_name_dictionary = new NSDictionary ();
                NSAttributedString selected_name_attributes = new NSAttributedString (
                    selected_name_data, new NSUrl ("file://"), out selected_name_dictionary);

                NSData selected_description_data = NSData.FromString (
                    "<small><font style='line-height: 150%' color='#9bbaeb' face='Lucida Grande'>" +
                    plugin.Description + "</font></small>");

                NSDictionary selected_description_dictionary       = new NSDictionary ();
                NSAttributedString selected_description_attributes = new NSAttributedString (
                    selected_description_data, new NSUrl ("file://"), out selected_description_dictionary);

                NSMutableAttributedString selected_mutable_attributes =
                    new NSMutableAttributedString (selected_name_attributes);

                selected_mutable_attributes.Append (new NSAttributedString ("\n"));
                selected_mutable_attributes.Append (selected_description_attributes);

                selected_cell.AttributedStringValue = selected_mutable_attributes;
                SelectedCells [i] = (NSAttributedString) selected_cell.ObjectValue;

                i++;
            }
        }


        [Export("numberOfRowsInTableView:")]
        public int numberOfRowsInTableView (NSTableView table_view)
        {
            if (Items == null)
                return 0;
            else
                return Items.Count;
        }


        [Export("tableView:objectValueForTableColumn:row:")]
        public NSObject objectValueForTableColumn (NSTableView table_view,
            NSTableColumn table_column, int row_index)
        {
            if (table_column.HeaderToolTip.Equals ("Description")) {
                if (table_view.SelectedRow == row_index &&
                    Program.UI.Setup.IsKeyWindow &&
                    Program.UI.Setup.FirstResponder == table_view) {

                    return SelectedCells [row_index];

                } else {
                    return Cells [row_index];
                }

            } else {
                return new NSImage ((Items [row_index] as SparklePlugin).ImagePath) {
                    Size = new SizeF (24, 24)
                };
            }
        }
    }


    public class SparkleTextFieldDelegate : NSTextFieldDelegate {

        public event StringValueChangedHandler StringValueChanged;
        public delegate void StringValueChangedHandler ();


        public override void Changed (NSNotification notification)
        {
            if (StringValueChanged != null)
                StringValueChanged ();
        }
    }


    public class SparkleTableViewDelegate : NSTableViewDelegate {

        public event SelectionChangedHandler SelectionChanged;
        public delegate void SelectionChangedHandler ();


        public override void SelectionDidChange (NSNotification notification)
        {
            if (SelectionChanged != null)
                SelectionChanged ();
        }
    }
}