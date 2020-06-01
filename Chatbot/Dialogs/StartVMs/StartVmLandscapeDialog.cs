using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chatbot.AzureHandling;
using Chatbot.Dialogs.Framework;
using Chatbot.Objects;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;

namespace Chatbot.Dialogs.StartVMs
{
    public class StartVmLandscapeDialog : CancelAndHelpDialog
    {
        public StartVmLandscapeDialog() : base(nameof(StartVmLandscapeDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetLandscapeStepAsync,
                GetChoiceStepAsync,
                ObligationOrAllStepAsync,
                CheckForObligationVmsStepAsync,
                PassOnStepAsync,
                ConfirmStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetLandscapeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;
            details.RunWithCompleteLandscape = true;
            stepContext.Values["details"] = details;

            if (details.LandscapeTag == null)
            {
                var message = "Welche Landschaft soll gestartet werden?" + " Bitte wähle aus." + Environment.NewLine;

                IList<Choice> choices = new List<Choice>();
                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                var landscapes = handler.GetAllAvailableLandscapes();
                if (!landscapes.Any())
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Es existieren keine Landschaften in ihrem Azure Account. Diese müssen erst konfiguriert werden"), cancellationToken);
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
                foreach (var tag in landscapes)
                {
                    choices.Add(new Choice(tag));
                }

                message = message + "Folgende Landschaften können von dir gestartet werden:";

                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage }, cancellationToken);
            }

            return await stepContext.NextAsync(new FoundChoice() { Value = details.LandscapeTag }, cancellationToken);
        }

        private async Task<DialogTurnResult> GetChoiceStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];

            if (stepContext.Result is FoundChoice choice)
            {
                details.LandscapeTag = choice.Value;
                stepContext.Values["details"] = details;
            }

            return await stepContext.NextAsync(details, cancellationToken);
        }

        private async Task<DialogTurnResult> ObligationOrAllStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Result;
            stepContext.Values["details"] = details;

            IList<Choice> choices = new List<Choice>();
            choices.Add(new Choice("Nur die notwendigen VMs"));
            choices.Add(new Choice("Alle VMs"));

            var message = "Welche VMs der Landschaft " + details.LandscapeTag + " sollen gestartet werden?";
            var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage, Style = ListStyle.SuggestedAction }, cancellationToken);
        }

        private async Task<DialogTurnResult> CheckForObligationVmsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
            var choice = (FoundChoice)stepContext.Result;

            if (choice.Value == "Alle VMs")
            {
                return await stepContext.NextAsync(details, cancellationToken);
            }
            else
            {
                details.OnlyObligationVms = true;
                stepContext.Values["details"] = details;

                var landscapeObligationToStartList = handler.GetObligationVmsForLandscapeTagAsList(details.LandscapeTag);
                if (landscapeObligationToStartList.Count > 0)
                {
                    var message = "Folgende VMs sind als notwendig hinterlegt worden:";
                    foreach (var vm in landscapeObligationToStartList)
                    {
                        message = message + Environment.NewLine + vm;
                    }
                    message = message + Environment.NewLine + "Falls diese Auswahl nicht stimmen sollte, muss dies umkonfiguriert werden";
                    var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions() { Prompt = promptMessage }, cancellationToken);
                }
                else
                {
                    var message = "Für diese Landschaft existiert keine Konfiguration für notwenige VMs. Solange die Konfiguration nicht existiert, werden alle VMs der Landschaft gestartet.";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message, InputHints.IgnoringInput), cancellationToken);
                    return await stepContext.NextAsync(details, cancellationToken);
                }
            }
        }

        private async Task<DialogTurnResult> PassOnStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result is ChatbotDetails details)
            {
                return await stepContext.NextAsync(details, cancellationToken);
            }
       
            return await stepContext.NextAsync(stepContext.Values["details"], cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Result;
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);

            var messageText = $"Bitte bestätige: Die Landschaft: {details.LandscapeTag} wird gestartet." + Environment.NewLine;
            messageText = messageText + "Folgende Vms werden gestartet: " + Environment.NewLine;
            if (details.OnlyObligationVms)
            {
                foreach (var vm in handler.GetObligationVmsForLandscapeTag(details.LandscapeTag))
                {
                    messageText = messageText + vm + Environment.NewLine;
                }
            }
            else
            {
                foreach (var vm in handler.GetListOfVmsFromSpecificLandscapeTag(details.LandscapeTag))
                {
                    messageText = messageText + vm + Environment.NewLine;
                }
            }

            messageText = messageText + "Ist das korrekt?";
            stepContext.Values["details"] = details;
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);

            if ((bool)stepContext.Result)
            {

                details.RunWithCompleteLandscape = true;
                handler.StartVmsAsync(details);

                return await stepContext.EndDialogAsync(details, cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
