using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using System;
using Microsoft.Bot.Builder.Ai.LUIS;
using Microsoft.Bot.Builder.Ai.QnA;
using BotBuilder4KnowledgeBot.Services;
using Microsoft.Bot.Builder.Ai.Translation;
using Microsoft.Bot.Builder.TraceExtensions;
using BotBuilder4KnowledgeBot.Models;

namespace BotBuilder4KnowledgeBot
{
    public class KnowledgeBot : IBot
    {
        /// <summary>
        /// The client for searching with Azure Search
        /// </summary>
        SearchIndexClient indexClient;
        /// <summary>
        /// Contains information from the appsettings file
        /// </summary>
        IConfiguration configuration;
        /// <summary>
        /// The client to translate and detect language using Microsoft translator
        /// </summary>
        Translator translatorClient;
        /// <summary>
        /// The client for retrieving answers from QnA maker
        /// </summary>
        QnAMaker qnaClient;
        /// <summary>
        /// The client for getting the users intent from LUIS
        /// </summary>
        LuisModel botLUISModel;
        /// <summary>
        /// The users native language, either German ("de") or another language detected with Microosft translator
        /// </summary>
        string userNativeLanguage = "de";

        public KnowledgeBot(IConfiguration config)
        {
            this.configuration = config;
            indexClient = AzureSearchService.CreateSearchIndexClient(configuration["SearchServiceName"], configuration["SearchDialogsIndexName"], configuration["SearchServiceQueryApiKey"]);
            qnaClient = new QnAMaker(new QnAMakerEndpoint() { EndpointKey = configuration["QnAKey"], Host = configuration["QnAEndpoint"], KnowledgeBaseId = configuration["QnAKnowledgeBaseId"] });
            translatorClient = new Translator(configuration["TranslationAPI"]);
            botLUISModel = new LuisModel(configuration["LUISAppId"], configuration["LUISApiKey"], new Uri(configuration["LUISEndpointUri"]));
        }

        public async Task OnTurn(ITurnContext context)
        {
            switch (context.Activity.Type)
            {
                // On "conversationUpdate"-type activities this bot will send a greeting message to users joining the conversation.
                case ActivityTypes.ConversationUpdate:
                    var newUserName = context.Activity.MembersAdded.FirstOrDefault()?.Name;
                    if (!string.IsNullOrWhiteSpace(newUserName) && newUserName != "Bot")
                    {
                        await context.SendActivity($"Hallo {newUserName}, ich bin ein Bot. Zu welchem Thema kann ich weiterhelfen?");
                    }

                    break;

                case ActivityTypes.Message:

                    var userInputGerman = context.Activity.Text;

                        // Use translation for each message
                        // Detect user language
                        userNativeLanguage = await translatorClient.Detect(context.Activity.Text);

                        // Just for tracing output
                        var translationInfoOutput = await translatorClient.Translate($"Test der Übersetzung\nSprache der Eingabe: ", "de", userNativeLanguage);

                        // Just translate if the text is in a laguage other than German
                        if (!userNativeLanguage.Equals("de"))
                        {
                            userInputGerman = await translatorClient.Translate(context.Activity.Text, userNativeLanguage, "de");

                            // Add tracing output
                            translationInfoOutput += $"{userNativeLanguage}\n";
                            translationInfoOutput += await translatorClient.Translate("Übersetzung der Eingabe", "de", userNativeLanguage);
                            translationInfoOutput += $": {userInputGerman}";

                        }
                        else translationInfoOutput += userNativeLanguage;

                        // Trace language and possible translation for debugging
                        await context.TraceActivity(translationInfoOutput);

                        // LUIS Start
                        var (intents, entities) = await RecognizeAsync(botLUISModel, userInputGerman);
                        var topIntent = intents.FirstOrDefault();
                        switch ((topIntent != null) ? topIntent : null)
                        {
                            case null:
                                await context.SendActivity(await translatorClient.Translate("Ich konnte dich leider nicht verstehen.", "de", userNativeLanguage));
                                break;
                            case "None":
                                await context.SendActivity(await translatorClient.Translate("Es tut mir leid, aber dabei kann ich leider nicht helfen.", "de", userNativeLanguage));
                                break;
                            case "Help":
                                await context.SendActivity(await translatorClient.Translate("Ich kann gerne versuchen zu helfen.", "de", userNativeLanguage));
                                break;
                            case "Weather":
                                await context.SendActivity(await translatorClient.Translate("Das Wetter ist großartig. Zumindest in meinem Rechenzentrum.", "de", userNativeLanguage));
                                break;
                            case "TechnicalQuestion":
                                await askQnAService(context, userInputGerman);
                                await useAzureSearch(context);
                                break;
                            case "Greeting":
                                await context.SendActivity(await translatorClient.Translate("Wie kann ich dir weiterhelfen?", "de", userNativeLanguage));
                                break;
                            default:
                                // Received an intent we didn't expect, so send its name and score for debugging.
                                await context.SendActivity(await translatorClient.Translate($"War das dein Anliegen?", "de", userNativeLanguage) + $" Intent: {topIntent}");
                                break;
                        }
                        // LUIS End

                    
                    break;
            }
        }

        private async Task useAzureSearch(ITurnContext context)
        {
            // Configure the parementers for the search through the Azure Search Service
            SearchParameters parameters =
                 new SearchParameters()
                 {
                     SearchMode = SearchMode.Any,
                     Select = new[] { "content", "language", "metadata_storage_path", "keyphrases" }
                 };

            // Get the documents through Azure search using the AzureSearchService service class
            DocumentSearchResult<SearchDocument> searchResults = AzureSearchService.RunQuery(context.Activity.Text, indexClient, parameters);

            if (searchResults != null && searchResults.Results.Count > 0)
            {
                // Output information on the first (most relevant) document)
                await context.SendActivity(await translatorClient.Translate($"Ich habe {searchResults.Results.Count} Dokumente zu diesem Thema gefunden. Das ist der Anfang der Dokumentes.", "de", userNativeLanguage));
                await context.SendActivity(searchResults.Results[0].Document.content.Substring(0, 400));
                string url = AzureSearchService.DecodeUrl(searchResults.Results[0].Document.metadata_storage_path);
                await context.SendActivity(await translatorClient.Translate($"Hier kannst du das Dokument herunterladen: {url}", "de", userNativeLanguage));
                // If there are more results, just display some links.
                if (searchResults.Results.Count > 1)
                {
                    string moreDocs = String.Empty;
                    moreDocs += await translatorClient.Translate("Weitere passende Dokumente:", "de", userNativeLanguage);
                    moreDocs += "\n";
                    // Display up to five more links
                    for (int i = 1; i < searchResults.Results.Count; i++)
                    {
                        moreDocs += AzureSearchService.DecodeUrl(searchResults.Results[i].Document.metadata_storage_path) + "\n";
                        if (i > 5) break;
                    }
                    await context.SendActivity(moreDocs);
                }
            }
            else
            {
                await context.SendActivity(await translatorClient.Translate("Ich habe zu diesem Suchbegriff leider kein Dokument gefunden. 😥", "de", userNativeLanguage));
            }
        }

        /// <summary>
        /// A helper method to get an answer from QnA maker and post it to the user
        /// </summary>
        /// <param name="context">The current current conversation context</param>
        /// <param name="userInput">The (translated) input from the user</param>
        /// <returns></returns>
        private async Task askQnAService(ITurnContext context, string userInput)
        {
            var qnaanswers = await qnaClient.GetAnswers(userInput);
            if (qnaanswers.Any())
            {
                await context.SendActivity((await translatorClient.Translate(qnaanswers.First().Answer, "de", userNativeLanguage)));
                await context.SendActivity(await translatorClient.Translate($"Ich hoffe das hilft Dir weiter. Ich habe in Summe {qnaanswers.Length} Antworten gefunden. Ich schaue auch noch nach passenden Dokumenten zu diesem Thema.", "de", userNativeLanguage));
            }
            else
            {
                await context.SendActivity(await translatorClient.Translate($"Leider habe ich darauf keine detailierte Antwort.Ich prüfe, ob ich Dokumente dazu finde.", "de", userNativeLanguage));
            }
        }

        /// <summary>
        /// A helper method that retrieves intents for an input from QnA maker
        /// </summary>
        /// <param name="luisModel">The LUIS model to be used for intent recognition</param>
        /// <param name="text">The text to find out the intent for</param>
        /// <returns></returns>
        private static async Task<(IEnumerable<string> intents, IEnumerable<string> entities)> RecognizeAsync(LuisModel luisModel, string text)
        {
            var luisRecognizer = new LuisRecognizer(luisModel);
            var recognizerResult = await luisRecognizer.Recognize(text, System.Threading.CancellationToken.None);

            // list the intents
            var intents = new List<string>();
            foreach (var intent in recognizerResult.Intents)
            {
                intents.Add(intent.Key);
            }

            // list the entities
            var entities = new List<string>();
            foreach (var entity in recognizerResult.Entities)
            {
                if (!entity.Key.ToString().Equals("$instance"))
                {
                    entities.Add($"{entity.Key}: {entity.Value.First}");
                }
            }

            return (intents, entities);
        }
    }
}

