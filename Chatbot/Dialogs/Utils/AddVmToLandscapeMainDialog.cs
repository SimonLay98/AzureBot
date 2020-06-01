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

namespace Chatbot.Dialogs.Utils
{
    public class AddVmToLandscapeMainDialog : CancelAndHelpDialog
    {
        public AddVmToLandscapeMainDialog() : base(nameof(AddVmToLandscapeMainDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new AddVmToLandscapeActDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
              StartStepAsync,
              AskForVmStepAsync,
              ActStepAsync,
              FinalStepAsync
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> StartStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;
            stepContext.Values["details"] = details;
            if (details.RecursionDialog)
            {
                details.RecursionDialog = false;
                var message = "Soll eine weitere VM zu einer Landschaft hinzugefügt werden?";
                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(true, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> AskForVmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];
            if ((bool)stepContext.Result)
            {
                var list = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token).GetListOfVms().Select(x => x.Name).ToList();

                IList<Choice> choices = new List<Choice>();

                foreach (var vm in list)
                {
                    choices.Add(new Choice(vm));
                }

                choices.Add(new Choice("Keine"));

                var message = "Die folgenden Vms können zu einer Landschaft hinzugefügt werden" + Environment.NewLine;
                message = message + "Welche soll zu einer Landschaft hinzugefügt werden?";

                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage }, cancellationToken);
            }
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Es wird keine VM zur Landschaft hinzugefügt."), cancellationToken);
            return await stepContext.EndDialogAsync(details, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];

            if (stepContext.Result is FoundChoice choice)
            {
                if (choice.Value == "Keine")
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Es wird keine VM zur Landschaft hinzugefügt."), cancellationToken);
                    return await stepContext.EndDialogAsync(details, cancellationToken);
                }
                else
                {
                    details.VmName = choice.Value;
                    return await stepContext.BeginDialogAsync(nameof(AddVmToLandscapeActDialog), details, cancellationToken);
                }
            }
            return await stepContext.EndDialogAsync(details, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Result;
            details.RecursionDialog = true;
            return await stepContext.ReplaceDialogAsync(InitialDialogId, details, cancellationToken);
        }
    }
}
