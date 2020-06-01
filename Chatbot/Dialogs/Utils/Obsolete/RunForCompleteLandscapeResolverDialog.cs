using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chatbot.AzureHandling;
using Chatbot.Dialogs.Framework;
using Chatbot.Objects;
using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;

namespace Chatbot.Dialogs.Utils.Obsolete
{
    [Obsolete]
    public class RunForCompleteLandscapeResolverDialog : CancelAndHelpDialog
    {
        public RunForCompleteLandscapeResolverDialog(string id = null) : base(id ?? nameof(RunForCompleteLandscapeResolverDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new AddVmToLandscapeActDialog());
            AddDialog(new SetObligationVmsForLandscapeDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                LandscapeStepAsync,
                ObligationOrAllStepAsync,
                ObligationVmsStepAsync,
                ObligationVmsStep2Async,
                FinalStepAsync
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> LandscapeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
            var tags = handler.GetListOfVms().FirstOrDefault(x => x.Name == details.VmName)?.Tags;
            if (tags == null)
            {
                return await stepContext.BeginDialogAsync(nameof(AddVmToLandscapeActDialog), details, cancellationToken);
            }

            var landscapeTag = tags.FirstOrDefault(x => x.Key == "Landschaft").Value;
            details.LandscapeTag = landscapeTag;

            if (string.IsNullOrWhiteSpace(landscapeTag))
            {
                details.RunWithCompleteLandscape = true;
                return await stepContext.BeginDialogAsync(nameof(AddVmToLandscapeActDialog), details, cancellationToken);
            }
            else
            {
                var message = "Die ausgewählte VM hat folgende Tags:" + Environment.NewLine;
                foreach (var tag in tags)
                {
                    message = message + tag + Environment.NewLine;
                }

                stepContext.Values["details"] = details;
                message = message + " Soll der Vorgang für mehrere VMs der Landschaft: " + landscapeTag + " ausgeführt werden?";
                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ObligationOrAllStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            ChatbotDetails details = new ChatbotDetails();
            //Hier kommt er aus einem anderen Dialog zurück
            if (stepContext.Result is ChatbotDetails d)
            {
                details = d;
                if (!d.RunWithCompleteLandscape)
                {
                    return await stepContext.EndDialogAsync(details, cancellationToken);
                }
            }
            if (stepContext.Result is bool b)
            {
                details = (ChatbotDetails)stepContext.Values["details"];
                //Frage mit ja beantwortet
                if (b)
                {
                    details.RunWithCompleteLandscape = true;
                }
                //Frage mit Nein beantwortet->keine ganze Landschaft also Dialog wieder verlassen
                else
                {
                    details.RunWithCompleteLandscape = false;
                    return await stepContext.EndDialogAsync(details, cancellationToken);
                }
            }

            if (details.Intent == AzureBotLuis.Intent.ShutDownSingleVm)
            {
                return await stepContext.EndDialogAsync(details, cancellationToken);
            }

            stepContext.Values["details"] = details;

            IList<Choice> choices = new List<Choice>();
            choices.Add(new Choice("Nur die notwendigen VMs"));
            choices.Add(new Choice("Alle VMs"));

            var message = "Für welche VMs der Landschaft " + details.LandscapeTag + " soll der Vorgang durchgeführt werden?";
            var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage, Style = ListStyle.SuggestedAction }, cancellationToken);
        }


        private async Task<DialogTurnResult> ObligationVmsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
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
                    var message = "Folgende VMs sind von dir als notwendig hinterlegt worden:";
                    foreach (var vm in landscapeObligationToStartList)
                    {
                        message = message + Environment.NewLine + vm;
                    }
                    message = message + Environment.NewLine + "Soll diese Auswahl erweitert oder eingegrenzt werden?";
                    var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions() { Prompt = promptMessage }, cancellationToken);
                }
                else
                {
                    return await stepContext.BeginDialogAsync(nameof(SetObligationVmsForLandscapeDialog), details, cancellationToken);
                }
            }
        }

        private async Task<DialogTurnResult> ObligationVmsStep2Async(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result is ChatbotDetails details)
            {
                return await stepContext.NextAsync(details, cancellationToken);
            }
            else if (stepContext.Result is bool result && result)
            {
                return await stepContext.BeginDialogAsync(nameof(SetObligationVmsForLandscapeDialog), stepContext.Values["details"], cancellationToken);
            }

            return await stepContext.NextAsync(stepContext.Values["details"], cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = stepContext.Result;

            return await stepContext.EndDialogAsync(details, cancellationToken);
        }
    }
}

