using Caly.Core.Models.Ai;
using Caly.Core.Services.Ai;

namespace Caly.Tests
{
    public class AiResultParserTests
    {
        private const string Json =
            """{"items":[{"kind":"table","page":2,"box":[0.1,0.2,0.5,0.6],"csv":"a,b\n1,2"}]}""";

        [Fact]
        public void Parses_plain_json()
        {
            var item = Assert.Single(AiResultParser.ParseExtraction(Json, 1));

            Assert.Equal(AiItemKind.Table, item.Kind);
            Assert.Equal(2, item.PageNumber);
            Assert.Equal(new AiBox(0.1, 0.2, 0.5, 0.6), item.Box);
            Assert.Equal("a,b\n1,2", item.Content);
        }

        [Fact]
        public void Parses_fenced_json()
        {
            var item = Assert.Single(AiResultParser.ParseExtraction("Here you go:\n```json\n" + Json + "\n```\nDone.", 1));

            Assert.Equal(AiItemKind.Table, item.Kind);
            Assert.Equal("a,b\n1,2", item.Content);
        }

        [Fact]
        public void Prose_becomes_single_text_item_on_fallback_page()
        {
            var item = Assert.Single(AiResultParser.ParseExtraction("There is no table here.", 7));

            Assert.Equal(AiItemKind.Text, item.Kind);
            Assert.Equal(7, item.PageNumber);
            Assert.Null(item.Box);
            Assert.Equal("There is no table here.", item.Content);
        }

        [Fact]
        public void Unterminated_fence_does_not_throw()
        {
            var items = AiResultParser.ParseExtraction("```json\n{\"items\":[{\"kind\":\"tab", 3);

            var item = Assert.Single(items);
            Assert.Equal(AiItemKind.Text, item.Kind);
            Assert.Equal(3, item.PageNumber);
        }

        [Fact]
        public void Empty_reply_gives_no_items()
        {
            Assert.Empty(AiResultParser.ParseExtraction("  \n", 1));
        }

        [Fact]
        public void Box_is_clamped_to_unit_square()
        {
            var item = Assert.Single(AiResultParser.ParseExtraction(
                """{"items":[{"kind":"chart","page":1,"box":[-0.5,0.2,1.5,0.6],"csv":"x"}]}""", 1));

            Assert.Equal(new AiBox(0, 0.2, 1, 0.6), item.Box);
        }

        [Fact]
        public void Inverted_box_is_swapped_and_empty_box_dropped()
        {
            var items = AiResultParser.ParseExtraction(
                """{"items":[{"kind":"table","page":1,"box":[0.5,0.6,0.1,0.2],"csv":"x"},{"kind":"table","page":1,"box":[0.3,0.3,0.3,0.9],"csv":"y"},{"kind":"table","page":1,"box":[0.1,0.2],"csv":"z"}]}""", 1);

            Assert.Equal(new AiBox(0.1, 0.2, 0.5, 0.6), items[0].Box);
            Assert.Null(items[1].Box);
            Assert.Null(items[2].Box);
        }

        [Fact]
        public void Missing_page_uses_fallback()
        {
            var item = Assert.Single(AiResultParser.ParseExtraction("""{"items":[{"kind":"table","csv":"x"}]}""", 9));

            Assert.Equal(9, item.PageNumber);
        }

        [Fact]
        public void Csv_file_keeps_quoted_cells_with_commas_quotes_newlines()
        {
            const string csv = "a,\"b, \"\"q\"\"\nc\"\n1,2";
            var file = AiResultParser.ToCsvFile([new AiResultItem(AiItemKind.Table, 1, null, csv)]);

            Assert.Equal(csv, file);
            Assert.Equal([["a", "b, \"q\"\nc"], ["1", "2"]], ReadCsv(file));
        }

        [Fact]
        public void Items_are_separated_by_one_blank_line()
        {
            var file = AiResultParser.ToCsvFile(
            [
                new AiResultItem(AiItemKind.Table, 1, null, "a,b\r\n1,2\r\n"),
                new AiResultItem(AiItemKind.Chart, 2, null, "x,y\n3,4")
            ]);

            Assert.Equal("a,b\n1,2\n\nx,y\n3,4", file);
        }

        private static List<List<string>> ReadCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; }
                else cell.Append(c);
            }
            row.Add(cell.ToString());
            rows.Add(row);
            return rows;
        }
    }
}
