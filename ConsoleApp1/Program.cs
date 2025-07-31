using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using System.Text;
using Document = iTextSharp.text.Document;

namespace PDFGenerator
{
    public class ContentSource
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public class PDFContentGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;
        private readonly List<string> _contentSources;
        private readonly SemaphoreSlim _semaphore;
        private int _documentNumber;

        public PDFContentGenerator()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _random = new Random();
            _semaphore = new SemaphoreSlim(10, 10); // Limit concurrent requests

            // Add various content sources
            _contentSources = new List<string>
            {
                "https://en.wikipedia.org/wiki/Special:Random",
                "https://www.gutenberg.org/browse/recent/last1",
                "https://quotegarden.com/",
                "https://www.poetryfoundation.org/poems/browse",
                "https://www.brainyquote.com/",
                "https://en.wikiquote.org/wiki/Main_Page",
                "https://www.goodreads.com/quotes",
                "https://www.ancient.eu/",
                "https://plato.stanford.edu/",
                "https://www.smithsonianmag.com/",
            };
        }

        public async Task GeneratePDFsAsync(int count, string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var tasks = new List<Task>();

            for (int i = 0; i < count; i++)
            {
                int fileIndex = i;
                tasks.Add(GenerateSinglePDFAsync(fileIndex, outputDirectory));

                // Process in batches to avoid overwhelming the system
                if (tasks.Count >= 100)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                    Console.WriteLine($"Generated {fileIndex + 1} PDFs...");
                }
            }

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        private async Task GenerateSinglePDFAsync(int index, string outputDirectory)
        {
            _documentNumber = index; // Set the document number for unique content generation

            await _semaphore.WaitAsync();

            try
            {
                var content = await GetRandomContentAsync();
                var fileName = Path.Combine(outputDirectory, $"document_{index:D6}.pdf");

                await CreatePDFAsync(fileName, content, index);

                // Check file size and regenerate if too large
                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Length > 1024 * 1024) // 1MB
                {
                    // Truncate content and regenerate
                    content.Content = TruncateContent(content.Content, 800000); // ~800KB for safety
                    await CreatePDFAsync(fileName, content, index);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<ContentSource> GetRandomContentAsync()
        {
            try
            {
                // Try multiple sources and fallback to generated content if needed
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var content = await FetchWebContentAsync();
                        if (!string.IsNullOrEmpty(content.Content))
                            return content;
                    }
                    catch
                    {
                        // Continue to next attempt or fallback
                    }
                }

                // Fallback to generated content
                return GenerateFallbackContent();
            }
            catch
            {
                return GenerateFallbackContent();
            }
        }

        private async Task<ContentSource> FetchWebContentAsync()
        {
            // Use free APIs and public content sources
            var sources = new[]
            {
                "https://api.quotable.io/random?minLength=150",
                "https://uselessfacts.jsph.pl/random.json?language=en",
                "https://official-joke-api.appspot.com/random_joke",
            };

            var source = sources[_random.Next(sources.Length)];

            try
            {
                var response = await _httpClient.GetStringAsync(source);

                if (source.Contains("quotable"))
                {
                    dynamic quote = JsonConvert.DeserializeObject(response);
                    return new ContentSource
                    {
                        Title = "Inspirational Quote",
                        Content = $"Quote: \"{quote.content}\"\n\nAuthor: {quote.author}\n\n" +
                                 GenerateAdditionalContent("philosophy", 2000),
                        Url = source
                    };
                }
                else if (source.Contains("uselessfacts"))
                {
                    dynamic fact = JsonConvert.DeserializeObject(response);
                    return new ContentSource
                    {
                        Title = "Interesting Fact",
                        Content = $"Did you know?\n\n{fact.text}\n\n" +
                                 GenerateAdditionalContent("science", 2500),
                        Url = source
                    };
                }
                else if (source.Contains("joke-api"))
                {
                    dynamic joke = JsonConvert.DeserializeObject(response);
                    return new ContentSource
                    {
                        Title = "Humor and Entertainment",
                        Content = $"Setup: {joke.setup}\n\nPunchline: {joke.punchline}\n\n" +
                                 GenerateAdditionalContent("humor", 2200),
                        Url = source
                    };
                }
            }
            catch
            {
                // Fallback handled by caller
            }

            return new ContentSource();
        }

        private ContentSource GenerateFallbackContent()
        {
            var topics = new[] { "technology", "science", "history", "literature", "philosophy", "art" };
            var topic = topics[_random.Next(topics.Length)];

            return new ContentSource
            {
                Title = $"Educational Content: {char.ToUpper(topic[0]) + topic.Substring(1)}",
                Content = GenerateAdditionalContent(topic, 3000),
                Url = "Generated Content"
            };
        }

        private string GenerateAdditionalContent(string topic, int targetLength)
        {
            var content = new StringBuilder();
            var sentenceCount = 0;

            // Generate unique sentences until we reach the target length
            while (content.Length < targetLength)
            {
                var uniqueSentence = GenerateUniqueSentence(topic, sentenceCount++);
                content.AppendLine(uniqueSentence);
                content.AppendLine();
            }

            return content.ToString();
        }

        private string GenerateUniqueSentence(string topic, int sentenceIndex)
        {
            // Use document number and sentence index to ensure uniqueness across all PDFs
            var seed = _documentNumber * 10000 + sentenceIndex;
            var rng = new Random(seed);

            var sentenceType = rng.Next(10);

            switch (sentenceType)
            {
                case 0:
                    return $"{GetIntroPhrase(rng)} {topic} {GetVerbPhrase(rng)} {GetObjectPhrase(rng)} {GetContextPhrase(rng)}.";
                case 1:
                    return $"{GetTimePhrase(rng)}, {GetSubjectPhrase(rng, topic)} {GetActionPhrase(rng)} {GetResultPhrase(rng)}.";
                case 2:
                    return $"{GetResearchPhrase(rng)} {topic} {GetFindingPhrase(rng)} {GetImplicationPhrase(rng)}.";
                case 3:
                    return $"{GetComparisonPhrase(rng)} {GetAspect(rng)} of {topic} {GetContrastPhrase(rng)} {GetConclusion(rng)}.";
                case 4:
                    return $"{GetExpertPhrase(rng)} {topic} {GetBeliefPhrase(rng)} {GetReasonPhrase(rng)}.";
                case 5:
                    return $"{GetMethodPhrase(rng)} {topic} {GetProcessPhrase(rng)} {GetOutcomePhrase(rng)}.";
                case 6:
                    return $"{GetChallengePhrase(rng)} {topic} {GetSolutionPhrase(rng)} {GetBenefitPhrase(rng)}.";
                case 7:
                    return $"{GetHistoricalPhrase(rng)} {topic} {GetDevelopmentPhrase(rng)} {GetImpactPhrase(rng)}.";
                case 8:
                    return $"{GetFuturePhrase(rng)} {topic} {GetPredictionPhrase(rng)} {GetPotentialPhrase(rng)}.";
                default:
                    return $"{GetAnalysisPhrase(rng)} {topic} {GetObservationPhrase(rng)} {GetSignificancePhrase(rng)}.";
            }
        }

        // Helper methods for generating unique phrases
        private string GetIntroPhrase(Random rng)
        {
            var phrases = new[]
            {
                "The comprehensive study of", "An in-depth analysis of", "Extensive research into",
                "The systematic examination of", "A thorough investigation of", "The detailed exploration of",
                "Critical evaluation of", "The scientific approach to", "Multidisciplinary studies of",
                "The theoretical framework of", "Empirical evidence regarding", "The foundational aspects of"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetVerbPhrase(Random rng)
        {
            var phrases = new[]
            {
                "demonstrates", "reveals", "indicates", "suggests", "establishes", "confirms",
                "challenges", "supports", "questions", "validates", "explores", "examines"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetObjectPhrase(Random rng)
        {
            var phrases = new[]
            {
                "complex relationships", "fundamental principles", "emerging patterns", "critical factors",
                "underlying mechanisms", "key variables", "essential components", "core concepts",
                "significant correlations", "important connections", "vital elements", "crucial aspects"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetContextPhrase(Random rng)
        {
            var phrases = new[]
            {
                "in contemporary settings", "across multiple domains", "within specific parameters",
                "under various conditions", "throughout different contexts", "in practical applications",
                "across diverse populations", "within controlled environments", "in real-world scenarios",
                "under optimal circumstances", "across temporal dimensions", "within theoretical bounds"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetTimePhrase(Random rng)
        {
            var phrases = new[]
            {
                "In recent years", "Over the past decade", "Throughout history", "In contemporary times",
                "During extensive studies", "Following rigorous analysis", "After careful consideration",
                "Through systematic observation", "Based on longitudinal research", "According to recent findings",
                "As documented extensively", "Through empirical investigation"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetSubjectPhrase(Random rng, string topic)
        {
            var phrases = new[]
            {
                $"researchers specializing in {topic}", $"experts in the field of {topic}",
                $"practitioners working with {topic}", $"scholars studying {topic}",
                $"professionals engaged in {topic}", $"scientists investigating {topic}",
                $"theorists examining {topic}", $"analysts focusing on {topic}",
                $"specialists in {topic}", $"authorities on {topic}",
                $"leading figures in {topic}", $"pioneers of {topic}"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetActionPhrase(Random rng)
        {
            var phrases = new[]
            {
                "have discovered", "have identified", "have developed", "have proposed",
                "have demonstrated", "have established", "have formulated", "have documented",
                "have observed", "have confirmed", "have validated", "have synthesized"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetResultPhrase(Random rng)
        {
            var phrases = new[]
            {
                "groundbreaking insights", "innovative approaches", "novel methodologies", "significant breakthroughs",
                "important discoveries", "valuable contributions", "meaningful advances", "substantial progress",
                "remarkable findings", "compelling evidence", "transformative solutions", "paradigm shifts"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetResearchPhrase(Random rng)
        {
            var phrases = new[]
            {
                "Quantitative analysis of", "Qualitative studies in", "Meta-analytical reviews of",
                "Experimental investigations into", "Observational research on", "Comparative studies of",
                "Longitudinal examination of", "Cross-sectional analysis of", "Systematic reviews of",
                "Controlled experiments in", "Field studies regarding", "Laboratory research on"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetFindingPhrase(Random rng)
        {
            var phrases = new[]
            {
                "consistently shows", "reliably indicates", "strongly suggests", "clearly demonstrates",
                "empirically confirms", "statistically validates", "conclusively proves", "effectively establishes",
                "systematically reveals", "comprehensively illustrates", "definitively supports", "robustly affirms"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetImplicationPhrase(Random rng)
        {
            var phrases = new[]
            {
                "far-reaching implications", "significant consequences", "important ramifications", "profound effects",
                "substantial impact", "meaningful outcomes", "critical results", "essential findings",
                "valuable insights", "practical applications", "theoretical contributions", "empirical support"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetComparisonPhrase(Random rng)
        {
            var phrases = new[]
            {
                "When comparing", "In contrast to", "Unlike traditional views of", "Compared with",
                "In relation to", "As opposed to", "Relative to", "In comparison with",
                "Contrasting with", "Juxtaposed against", "When evaluated against", "In parallel with"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetAspect(Random rng)
        {
            var aspects = new[]
            {
                "theoretical foundations", "practical applications", "methodological approaches", "empirical evidence",
                "conceptual frameworks", "operational definitions", "functional characteristics", "structural elements",
                "dynamic properties", "systemic features", "behavioral patterns", "organizational principles"
            };
            return aspects[rng.Next(aspects.Length)];
        }

        private string GetContrastPhrase(Random rng)
        {
            var phrases = new[]
            {
                "differs significantly from", "shows marked improvement over", "represents a departure from",
                "challenges conventional", "transcends traditional", "surpasses previous", "contradicts earlier",
                "complements existing", "enhances current", "refines established", "advances beyond", "builds upon"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetConclusion(Random rng)
        {
            var conclusions = new[]
            {
                "previous understanding", "established paradigms", "conventional wisdom", "traditional approaches",
                "existing methodologies", "current practices", "standard procedures", "accepted theories",
                "mainstream perspectives", "orthodox views", "prevailing assumptions", "dominant frameworks"
            };
            return conclusions[rng.Next(conclusions.Length)];
        }

        private string GetExpertPhrase(Random rng)
        {
            var phrases = new[]
            {
                "Leading authorities in", "Renowned specialists in", "Distinguished experts on",
                "Prominent researchers in", "Influential scholars of", "Notable practitioners in",
                "Respected professionals in", "Established leaders in", "Recognized authorities on",
                "Eminent scientists studying", "Pioneering investigators of", "Foremost experts in"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetBeliefPhrase(Random rng)
        {
            var phrases = new[]
            {
                "unanimously agree that", "collectively recognize that", "broadly acknowledge that",
                "generally concur that", "widely accept that", "commonly understand that",
                "frequently emphasize that", "consistently maintain that", "regularly assert that",
                "continually advocate that", "persistently argue that", "steadfastly believe that"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetReasonPhrase(Random rng)
        {
            var phrases = new[]
            {
                "evidence-based approaches yield superior results", "systematic methodologies ensure reliability",
                "comprehensive analysis provides deeper insights", "rigorous standards maintain quality",
                "interdisciplinary collaboration enhances outcomes", "innovative techniques drive progress",
                "empirical validation supports theories", "practical applications demonstrate value",
                "theoretical frameworks guide understanding", "methodological rigor ensures accuracy",
                "collaborative efforts maximize impact", "integrated approaches optimize results"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetMethodPhrase(Random rng)
        {
            var phrases = new[]
            {
                "The application of advanced techniques in", "Innovative methodologies for",
                "Cutting-edge approaches to", "State-of-the-art methods in",
                "Revolutionary techniques for", "Sophisticated procedures in",
                "Modern strategies for", "Contemporary methods of",
                "Progressive approaches in", "Evolving techniques for"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetProcessPhrase(Random rng)
        {
            var phrases = new[]
            {
                "involves complex procedures that", "requires careful coordination to",
                "necessitates systematic approaches that", "demands rigorous attention to",
                "incorporates multiple stages that", "utilizes iterative processes to",
                "employs sophisticated algorithms that", "integrates various components to",
                "combines diverse elements that", "orchestrates numerous factors to"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetOutcomePhrase(Random rng)
        {
            var phrases = new[]
            {
                "achieve optimal results", "maximize efficiency", "enhance performance",
                "improve outcomes", "optimize solutions", "streamline operations",
                "accelerate progress", "facilitate innovation", "drive transformation",
                "enable breakthroughs", "catalyze change", "promote advancement"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetChallengePhrase(Random rng)
        {
            var phrases = new[]
            {
                "Current challenges in", "Emerging obstacles within", "Complex problems facing",
                "Critical issues affecting", "Significant hurdles in", "Major difficulties concerning",
                "Pressing concerns about", "Fundamental challenges to", "Persistent problems in",
                "Ongoing struggles with", "Key barriers to", "Central dilemmas in"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetSolutionPhrase(Random rng)
        {
            var phrases = new[]
            {
                "require innovative solutions that", "demand creative approaches to",
                "necessitate strategic interventions that", "call for comprehensive strategies to",
                "benefit from collaborative efforts that", "respond to targeted initiatives that",
                "improve through systematic reforms that", "advance via coordinated actions that",
                "progress through dedicated programs that", "evolve with adaptive measures that"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetBenefitPhrase(Random rng)
        {
            var phrases = new[]
            {
                "deliver measurable improvements", "create lasting impact", "generate positive outcomes",
                "produce tangible results", "yield significant benefits", "foster sustainable growth",
                "enable continuous improvement", "support long-term success", "facilitate meaningful change",
                "promote holistic development", "ensure optimal performance", "drive competitive advantage"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetHistoricalPhrase(Random rng)
        {
            var phrases = new[]
            {
                "The historical development of", "The evolutionary trajectory of", "The chronological progression of",
                "The temporal evolution of", "The historical context surrounding", "The developmental history of",
                "The origins and evolution of", "The historical foundations of", "The temporal dimensions of",
                "The historical significance of", "The evolutionary aspects of", "The historical perspective on"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetDevelopmentPhrase(Random rng)
        {
            var phrases = new[]
            {
                "has undergone remarkable transformations that", "has experienced significant changes that",
                "has evolved through distinct phases that", "has progressed through various stages that",
                "has witnessed substantial developments that", "has adapted to changing circumstances that",
                "has responded to emerging needs that", "has matured through iterative processes that",
                "has advanced through innovative breakthroughs that", "has transformed in response to factors that"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetImpactPhrase(Random rng)
        {
            var phrases = new[]
            {
                "shaped contemporary understanding", "influenced modern practices", "transformed current approaches",
                "redefined fundamental concepts", "revolutionized traditional methods", "altered conventional thinking",
                "established new paradigms", "created lasting frameworks", "generated profound insights",
                "produced enduring contributions", "catalyzed significant advances", "enabled breakthrough discoveries"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetFuturePhrase(Random rng)
        {
            var phrases = new[]
            {
                "The future landscape of", "Emerging possibilities in", "Tomorrow's vision for",
                "The next generation of", "Anticipated developments in", "The forward trajectory of",
                "Projected advances in", "The coming evolution of", "Future innovations in",
                "The next phase of", "Forthcoming transformations in", "The future potential of"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetPredictionPhrase(Random rng)
        {
            var phrases = new[]
            {
                "promises unprecedented opportunities that", "suggests transformative possibilities that",
                "indicates revolutionary changes that", "points toward innovations that",
                "forecasts significant developments that", "anticipates breakthroughs that",
                "predicts substantial advances that", "envisions new horizons that",
                "projects remarkable growth that", "foresees paradigm shifts that"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetPotentialPhrase(Random rng)
        {
            var phrases = new[]
            {
                "could redefine industry standards", "may transform global perspectives",
                "will likely reshape fundamental approaches", "should revolutionize current practices",
                "might unlock new possibilities", "could enable unprecedented achievements",
                "may facilitate groundbreaking discoveries", "will potentially create new paradigms",
                "should generate transformative outcomes", "could catalyze systemic change"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetAnalysisPhrase(Random rng)
        {
            var phrases = new[]
            {
                "Comprehensive analysis of", "Detailed examination of", "Thorough investigation of",
                "Systematic evaluation of", "In-depth exploration of", "Critical assessment of",
                "Rigorous scrutiny of", "Careful consideration of", "Extensive review of",
                "Meticulous study of", "Precise analysis of", "Holistic examination of"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetObservationPhrase(Random rng)
        {
            var phrases = new[]
            {
                "reveals nuanced patterns that", "uncovers hidden relationships that",
                "identifies crucial factors that", "discovers unexpected connections that",
                "highlights important trends that", "exposes underlying dynamics that",
                "demonstrates complex interactions that", "illustrates fundamental principles that",
                "shows intricate dependencies that", "confirms theoretical predictions that"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string GetSignificancePhrase(Random rng)
        {
            var phrases = new[]
            {
                "contribute to comprehensive understanding", "enhance theoretical frameworks",
                "inform practical applications", "guide future research", "support evidence-based decisions",
                "enable predictive modeling", "facilitate strategic planning", "promote informed discourse",
                "advance scientific knowledge", "strengthen analytical capabilities"
            };
            return phrases[rng.Next(phrases.Length)];
        }

        private string TruncateContent(string content, int maxBytes)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            var bytes = Encoding.UTF8.GetBytes(content);
            if (bytes.Length <= maxBytes)
                return content;

            var truncated = Encoding.UTF8.GetString(bytes, 0, maxBytes);

            // Find the last complete word to avoid cutting mid-word
            var lastSpace = truncated.LastIndexOf(' ');
            if (lastSpace > 0)
                truncated = truncated.Substring(0, lastSpace);

            return truncated + "...";
        }

        private async Task CreatePDFAsync(string fileName, ContentSource content, int documentNumber)
        {
            using (var fs = new FileStream(fileName, FileMode.Create))
            {
                var document = new Document(PageSize.A4, 50, 50, 50, 50);
                var writer = PdfWriter.GetInstance(document, fs);

                document.Open();

                // Add title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.DARK_GRAY);
                var title = new Paragraph($"Document #{documentNumber + 1}: {content.Title}", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20f;
                document.Add(title);

                // Add metadata
                var metaFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);
                var meta = new Paragraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Source: {content.Url}", metaFont);
                meta.Alignment = Element.ALIGN_RIGHT;
                meta.SpacingAfter = 20f;
                document.Add(meta);

                // Add content
                var contentFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, BaseColor.BLACK);
                var contentParagraph = new Paragraph(content.Content, contentFont);
                contentParagraph.Alignment = Element.ALIGN_JUSTIFIED;
                contentParagraph.Leading = 14f;
                document.Add(contentParagraph);

                // Add footer
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, BaseColor.GRAY);
                var footer = new Paragraph($"\n\nDocument ID: DOC-{documentNumber:D6} | Page 1 of 1", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);

                document.Close();
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _semaphore?.Dispose();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("PDF Generator Starting...");
            Console.WriteLine("This will generate 10000 PDF files with valuable content from the internet.");
            Console.WriteLine("Each file will be limited to 1MB in size.\n");

            var outputDirectory = @"C:\Users\ukhan2\Desktop\pdf";

            Console.Write($"Output directory will be: {outputDirectory}\nPress Enter to continue or Ctrl+C to cancel...");
            Console.ReadLine();

            var generator = new PDFContentGenerator();

            try
            {
                var startTime = DateTime.Now;
                await generator.GeneratePDFsAsync(10000, outputDirectory);
                var endTime = DateTime.Now;

                Console.WriteLine($"\nGeneration completed!");
                Console.WriteLine($"Time taken: {endTime - startTime}");
                Console.WriteLine($"Files saved to: {outputDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                generator.Dispose();
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}