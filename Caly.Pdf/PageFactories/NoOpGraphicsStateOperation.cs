using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Operations;

namespace Caly.Pdf.PageFactories
{
    internal sealed class NoOpGraphicsStateOperation : IGraphicsStateOperation
    {
        public static readonly NoOpGraphicsStateOperation Instance = new();

        private NoOpGraphicsStateOperation()
        {
            // Private
        }

        public string Operator => string.Empty;

        public void Write(Stream stream)
        {

        }

        public void Run(IOperationContext operationContext)
        {

        }
    }
}
