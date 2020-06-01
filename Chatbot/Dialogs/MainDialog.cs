using System;
using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chatbot.AzureHandling;
using Chatbot.Dialogs.Framework;
using Chatbot.Dialogs.ShutDownVMs;
using Chatbot.Dialogs.StartVMs;
using Chatbot.Objects;


namespace Chatbot.Dialogs
{
    public class MainDialog : LogoutDialog
    {
        private readonly ControlAzureResourceRecognizer _luisRecognizer;
        private readonly IConfiguration _configuration;
        private readonly IStatePropertyAccessor<User> _userStateAccessors;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(ControlAzureResourceRecognizer luisRecognizer, StartVmDialog startVmDialog, UserState userState, IConfiguration configuration, ILogger<MainDialog> logger)
            : base(nameof(MainDialog), configuration["ConnectionName"])
        {
            _luisRecognizer = luisRecognizer;
            _configuration = configuration;
            _userStateAccessors = userState.CreateProperty<User>(nameof(User));

            AddDialog(new OAuthPrompt(nameof(OAuthPrompt), new OAuthPromptSettings
            {
                ConnectionName = ConnectionName,
                Text = "Kopiere den Code nach der erfolgreichen Anmeldung hier in den Chat",
                Title = "Bei Azure AD anmelden",
                Timeout = 100000,
            }));

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(startVmDialog);
            AddDialog(new ShutDownVmDialog());
            AddDialog(new StartVmLandscapeDialog());
            AddDialog(new ShutDownVmLandscapeDialog());
            AddDialog(new ConfigureDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginStepAsync,
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        private async Task<DialogTurnResult> LoginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result is TokenResponse tokenResponse)
            {
                JwtSecurityToken token = new JwtSecurityToken(tokenResponse.Token);
                var tenant = token.Payload["tid"].ToString();
                var name = token.Payload["name"];
                var uniqueName = token.Payload["unique_name"];

                var user = new User()
                {
                    TokenResponse = tokenResponse,
                    Mail = uniqueName.ToString(),
                    Name = name.ToString(),
                    Tenant = tenant
                };

                var conversationData = await _userStateAccessors.GetAsync(stepContext.Context, () => user, cancellationToken);

                conversationData.TokenResponse = tokenResponse;
                if (!conversationData.HasShownToken)
                {
                    conversationData.HasShownToken = true;
                    await _userStateAccessors.SetAsync(stepContext.Context, conversationData, cancellationToken);
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Angemeldet! Hallo " + name + ". Du kannst den Bot nun verwenden."), cancellationToken);
                    //await stepContext.Context.SendActivityAsync(MessageFactory.Text(tokenResponse.Token), cancellationToken); //Token uninteressant für den User
                }
                await _userStateAccessors.SetAsync(stepContext.Context, user, cancellationToken);
                return await stepContext.NextAsync(tokenResponse, cancellationToken);
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Kein Token und somit keine Anmeldung möglich"), cancellationToken);
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(
                        "Warnung: LUIS ist nicht konfiguriert. Der Bot geht davon aus du willst eine VM starten, für andere Intentionen" +
                        " muss LUIS konfiguriert sein", inputHint: InputHints.IgnoringInput), cancellationToken);

                return await stepContext.NextAsync(null, cancellationToken);
            }

            var messageText = stepContext.Options?.ToString() ?? "Wie kann ich dir helfen?\n Du kannst einzelne VMs oder komplette VM-Landschaften starten und wieder herunter fahren oder die Landschaften konfigurieren.";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                var noLuisDetails = new ChatbotDetails()
                {
                    Intent = AzureBotLuis.Intent.StartSingleVm,
                    User = _userStateAccessors.GetAsync(stepContext.Context, cancellationToken: cancellationToken).Result
                };

                // LUIS ist nicht konfiguriert, StartVmDialog wird gestartet
                return await stepContext.BeginDialogAsync(nameof(StartVmDialog), noLuisDetails, cancellationToken);
            }

            // Call LUIS and gather Intent and details (Note the TurnContext has the response to the prompt.)
            var luisResult = await _luisRecognizer.RecognizeAsync<AzureBotLuis>(stepContext.Context, cancellationToken);

            var details = new ChatbotDetails()
            {
                Intent = luisResult.TopIntent().intent,
                User = _userStateAccessors.GetAsync(stepContext.Context, cancellationToken: cancellationToken).Result
            };
            //TODO LUIS weiter trainieren und dafür sorgen das mehr Entities erkannt werden (evtl auf Score eingehen?)
            details = ShowWarningForUnsupportedVms(stepContext.Context, luisResult, details, cancellationToken).Result;

            switch (luisResult.TopIntent().intent)
            {
                case AzureBotLuis.Intent.StartSingleVm:
                    return await stepContext.BeginDialogAsync(nameof(StartVmDialog), details, cancellationToken);
                case AzureBotLuis.Intent.StartVmLandscape:
                    return await stepContext.BeginDialogAsync(nameof(StartVmLandscapeDialog), details, cancellationToken);
                case AzureBotLuis.Intent.ShutDownSingleVm:
                    return await stepContext.BeginDialogAsync(nameof(ShutDownVmDialog), details, cancellationToken);
                case AzureBotLuis.Intent.ShutDownVmLandscape:
                    return await stepContext.BeginDialogAsync(nameof(ShutDownVmLandscapeDialog), details, cancellationToken);
                case AzureBotLuis.Intent.Configure:
                    return await stepContext.BeginDialogAsync(nameof(ConfigureDialog), details, cancellationToken);
                default:
                    // Catch all for unhandled intents (gibt eigentlich keine unhandled intents)
                    var didntUnderstandMessageText =
                        $"Sorry, I didn't get that. Please try asking in a different way (intent was {luisResult.TopIntent().intent})";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // If the child dialog ("StartVmDialog") was cancelled, the user failed to confirm or if the intent wasn't another
            // the Result here will be null.
            if (stepContext.Result is ChatbotDetails result)
            {
                var messageText = "Konfiguration abgeschlossen";   //Wenn keine Konfiguration wird Wert überschrieben

                if (result.Intent == AzureBotLuis.Intent.StartSingleVm ||
                    result.Intent == AzureBotLuis.Intent.StartVmLandscape)
                {
                    messageText = "VM(s) erfolgreich gestartet";
                    messageText = messageText + Environment.NewLine + "Der Vorgang läuft im Hintergrund und kann mehrere Minuten pro VM dauern. Beende den Bot daher bitte nicht.";
                }

                if (result.Intent == AzureBotLuis.Intent.ShutDownVmLandscape ||
                    result.Intent == AzureBotLuis.Intent.ShutDownSingleVm)
                {
                    messageText = "VM(s) erfolgreich herunter gefahren";
                    messageText = messageText + Environment.NewLine + "Der Vorgang läuft im Hintergrund und kann mehrere Minuten pro VM dauern. Beende den Bot daher bitte nicht.";
                }

                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(message, cancellationToken);
            }
            else
            {
                var messageText = "Keine Aktion durchgeführt";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(message, cancellationToken);
            }

            // Restart the main dialog with a different message the second time around
            var promptMessage = "Was kann ich noch für dich tun?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }

        //endRegion Steps

        #region Methods

        private static async Task<ChatbotDetails> ShowWarningForUnsupportedVms(ITurnContext context, AzureBotLuis luisResult, ChatbotDetails details, CancellationToken cancellationToken)
        {
            var unsupportedVms = new List<string>();
            var unsupportedVmLandscapes = new List<string>();
            var azureHandler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);

            if (luisResult.Entities.VmLandscapeName != null)
            {
                if (!string.IsNullOrWhiteSpace(luisResult.Entities.VmLandscapeName[0]))
                {
                    var vmsWithLuisLandscapeTag = azureHandler.GetListOfVms().Where(x => x.Tags.Any(y => y.Key == "Landschaft" && y.Value == luisResult.Entities.VmLandscapeName[0]));

                    if (vmsWithLuisLandscapeTag.Any())
                    {
                        details.LandscapeTag = luisResult.Entities.VmLandscapeName[0];
                        details.RunWithCompleteLandscape = true;
                    }
                    else
                    {
                        unsupportedVmLandscapes.Add(luisResult.Entities.VmLandscapeName[0]);
                    }
                }
            }

            if (luisResult.Entities.VmName != null)
            {
                if (!string.IsNullOrWhiteSpace(luisResult.Entities.VmName[0]))
                {
                    var listOfVms = azureHandler.GetListOfVms().Select(x => x.Name).ToList();
                    if (listOfVms.Any(x => x == luisResult.Entities.VmName[0]))
                    {
                        details.VmName = luisResult.Entities.VmName[0];
                    }
                    else
                    {
                        unsupportedVms.Add(luisResult.Entities.VmName[0]);
                    }
                }
            }


            if (details.Intent == AzureBotLuis.Intent.StartSingleVm || details.Intent == AzureBotLuis.Intent.StartVmLandscape)
            {
                if (unsupportedVms.Any() || unsupportedVmLandscapes.Any())
                {
                    var messageText = "Du hast eine Vm oder Landschaft angegeben die nicht in deinem Azure Account zu finden war.";
                    var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                    await context.SendActivityAsync(message, cancellationToken);

                    foreach (var unsupportedLandscape in unsupportedVmLandscapes)
                    {
                        if (azureHandler.GetListOfVms().FirstOrDefault(x => x.Name == unsupportedLandscape) != null)
                        {
                            details.Intent = AzureBotLuis.Intent.StartSingleVm;
                            details.VmName = unsupportedLandscape;

                            var messageText2 = "Stattdessen wurde eine VM mit dem selben Namen gefunden. Ich gehen davon aus, das du diese gemeint hast.";
                            var message2 = MessageFactory.Text(messageText2, messageText2, InputHints.IgnoringInput);
                            await context.SendActivityAsync(message2, cancellationToken);
                        }
                    }

                    foreach (var unsupportedVm in unsupportedVms)
                    {
                        if (azureHandler.GetAllAvailableLandscapes().FirstOrDefault(x => x == unsupportedVm) != null)
                        {
                            details.Intent = AzureBotLuis.Intent.StartVmLandscape;
                            details.LandscapeTag = unsupportedVm;

                            var messageText2 = "Stattdessen wurde eine Landschaft mit dem selben Namen gefunden. Ich gehen davon aus, das du diese gemeint hast.";
                            var message2 = MessageFactory.Text(messageText2, messageText2, InputHints.IgnoringInput);
                            await context.SendActivityAsync(message2, cancellationToken);
                        }
                    }
                }
            }

            if (details.Intent == AzureBotLuis.Intent.ShutDownSingleVm || details.Intent == AzureBotLuis.Intent.ShutDownVmLandscape)
            {

                if (unsupportedVms.Any() || unsupportedVmLandscapes.Any())
                {
                    var messageText = "Du hast eine Vm oder Landschaft angegeben die nicht in deinem Azure Account zu finden sind.";
                    var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                    await context.SendActivityAsync(message, cancellationToken);

                    foreach (var unsupportedLandscape in unsupportedVmLandscapes)
                    {
                        if (azureHandler.GetListOfVms().FirstOrDefault(x => x.Name == unsupportedLandscape) != null)
                        {
                            details.Intent = AzureBotLuis.Intent.ShutDownSingleVm;
                            details.VmName = unsupportedLandscape;

                            var messageText2 = "Stattdessen wurde eine VM mit dem selben Namen gefunden. Ich gehen davon aus, das du diese gemeint hast.";
                            var message2 = MessageFactory.Text(messageText2, messageText2, InputHints.IgnoringInput);
                            await context.SendActivityAsync(message2, cancellationToken);
                        }
                    }

                    foreach (var unsupportedVm in unsupportedVms)
                    {
                        if (azureHandler.GetAllAvailableLandscapes().FirstOrDefault(x => x == unsupportedVm) != null)
                        {
                            details.Intent = AzureBotLuis.Intent.ShutDownVmLandscape;
                            details.LandscapeTag = unsupportedVm;

                            var messageText2 = "Stattdessen wurde eine Landschaft mit dem selben Namen gefunden. Ich gehen davon aus, das du diese gemeint hast.";
                            var message2 = MessageFactory.Text(messageText2, messageText2, InputHints.IgnoringInput);
                            await context.SendActivityAsync(message2, cancellationToken);
                        }
                    }
                }
            }

            return details;
        }

        #endregion

    }
}
