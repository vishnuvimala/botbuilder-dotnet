﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.FormDialogs;
using Microsoft.Bot.Builder.ComposableDialogs.Expressions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.FlowDialogs;

namespace Microsoft.Bot.Builder.FormDialogs.Tests
{
    [TestClass]
    public class FlowDialogTests
    {
        private static JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented };

        public IDialog CreateTestDialog()
        {
            var dialog = new ComponentDialog() { Id = "TestDialog" };

            // add prompts
            dialog.AddDialog(new NumberPrompt<Int32>());
            dialog.AddDialog(new TextPrompt());

            // define GetNameDialog
            var actionDialog = new FlowDialog()
            {
                Id = "GetNameDialog",
                CallDialogId = "TextPrompt",
                CallDialogOptions = new PromptOptions()
                {
                    Prompt = new Activity(type: ActivityTypes.Message, text: "What is your name?"),
                    RetryPrompt = new Activity(type: ActivityTypes.Message, text: "What is your name?")
                },
                OnCompleted = new CommandSet()
                {
                    Actions =
                    {
                        new SetVariable() { Name="Name", Value= new CSharpExpression("State.DialogTurnResult.Result")},
                        new Switch()
                        {
                            Condition = new CSharpExpression() { Expression="State.Name.Length > 2" },
                            Cases = new Dictionary<string, IFlowCommand>
                            {
                                { "true", new CallDialog("GetAgeDialog")  },
                                { "false", new ContinueDialog() }
                            },
                            DefaultAction = new SendActivity("default")
                        }
                    }
                }
            };
            dialog.InitialDialogId = actionDialog.Id;
            dialog.AddDialog(actionDialog);

            // define GetAgeDialog
            actionDialog = new FlowDialog()
            {
                Id = "GetAgeDialog",
                CallDialogId = "NumberPrompt",
                CallDialogOptions = new PromptOptions()
                {
                    Prompt = new Activity(type: ActivityTypes.Message, text: "What is your age?"),
                    RetryPrompt = new Activity(type: ActivityTypes.Message, text: "What is your age?")
                },
                OnCompleted = new CommandSet()
                {
                    Actions = {
                        new SetVariable() { Name = "Age", Value = new CSharpExpression("State.DialogTurnResult.Result") },
                        new SetVariable() { Name = "IsChild", Value = new CSharpExpression("State.Age < 18") },
                        new SendActivity() { Text = "Done" }
                    }
                }
            };
            dialog.AddDialog(actionDialog);

            return dialog;
        }

        [TestMethod]
        public async Task TestFlowDialog()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var dialogState = convoState.CreateProperty<DialogState>("dialogState");

            var adapter = new TestAdapter()
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger()))
                .Use(new AutoSaveStateMiddleware(convoState));

            var dialogs = new DialogSet(dialogState);

            var testDialog = CreateTestDialog();
            dialogs.Add(testDialog);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                var state = await dialogState.GetAsync(turnContext, () => new DialogState());

                var dialogContext = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dialogContext.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                    results = await dialogContext.BeginDialogAsync(testDialog.Id, null, cancellationToken);
            })
            .Send("hello")
            .AssertReply("What is your name?")
            .Send("x")
            .AssertReply("What is your name?")
            .Send("Joe")
            .AssertReply("What is your age?")
            .Send("64")
            .AssertReply("Done")
            .StartTestAsync();
        }

        //{

        //    var sd = new SwitchAction();
        //    sd.Condition = new ReflectionExpression("1 == 1");
        //    sd.Cases.Add("true", new TestAction("true"));
        //    sd.Cases.Add("false", new TestAction("false"));
        //    sd.DefaultAction = new TestAction("default");

        //    Assert.AreEqual(5, results.Count, "Should be 5 entities found");
        //    Assert.AreEqual(1, results.Where(entity => entity.Type == "age").Count(), "Should have 1 age entity");
        //}

    }
}