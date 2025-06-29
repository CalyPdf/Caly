using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.Compatibility;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using UglyToad.PdfPig.Graphics.Operations.TextObjects;
using UglyToad.PdfPig.Graphics.Operations.TextPositioning;
using UglyToad.PdfPig.Graphics.Operations.TextShowing;
using UglyToad.PdfPig.Graphics.Operations.TextState;
using UglyToad.PdfPig.Tokens;

namespace Caly.Pdf.PageFactories
{
    public sealed class TextOnlyGraphicsStateOperationFactory : IGraphicsStateOperationFactory
    {
        public static readonly TextOnlyGraphicsStateOperationFactory Instance = new TextOnlyGraphicsStateOperationFactory();

        private TextOnlyGraphicsStateOperationFactory()
        {
            // Private ctor
        }

        private static double[] TokensToDoubleArray(IReadOnlyList<IToken> tokens, bool exceptLast = false)
        {
            using var result = new ArrayPoolBufferWriter<double>(16);

            for (var i = 0; i < tokens.Count - (exceptLast ? 1 : 0); i++)
            {
                var operand = tokens[i];

                if (operand is ArrayToken arr)
                {
                    for (var j = 0; j < arr.Length; j++)
                    {
                        var innerOperand = arr[j];

                        if (!(innerOperand is NumericToken innerNumeric))
                        {
                            return result.WrittenSpan.ToArray();
                        }

                        result.Write(innerNumeric.Data);
                    }
                }

                if (!(operand is NumericToken numeric))
                {
                    return result.WrittenSpan.ToArray();
                }

                result.Write(numeric.Data);
            }

            return result.WrittenSpan.ToArray();
        }

        private static int OperandToInt(IToken token)
        {
            if (!(token is NumericToken numeric))
            {
                throw new InvalidOperationException($"Invalid operand token encountered when expecting numeric: {token}.");
            }

            return numeric.Int;
        }

        private static double OperandToDouble(IToken token)
        {
            if (!(token is NumericToken numeric))
            {
                throw new InvalidOperationException($"Invalid operand token encountered when expecting numeric: {token}.");
            }

            return numeric.Data;
        }

        public IGraphicsStateOperation? Create(OperatorToken op, IReadOnlyList<IToken> operands)
        {
            switch (op.Data)
            {
                case Type3SetGlyphWidth.Symbol:
                case Type3SetGlyphWidthAndBoundingBox.Symbol:
                    throw new NotImplementedException(op.Data);

                //case ModifyClippingByEvenOddIntersect.Symbol:
                //    return ModifyClippingByEvenOddIntersect.Value;
                //case ModifyClippingByNonZeroWindingIntersect.Symbol:
                //    return ModifyClippingByNonZeroWindingIntersect.Value;
                case BeginCompatibilitySection.Symbol:
                    return BeginCompatibilitySection.Value;
                case EndCompatibilitySection.Symbol:
                    return EndCompatibilitySection.Value;
                //case SetColorRenderingIntent.Symbol:
                //    return new SetColorRenderingIntent((NameToken)operands[0]);
                //case SetFlatnessTolerance.Symbol:
                //    if (operands.Count == 0)
                //    {
                //        return null; // Should not happen by definition
                //    }
                //    return new SetFlatnessTolerance(OperandToDouble(operands[0]));
                //case SetLineCap.Symbol:
                //    return new SetLineCap(OperandToInt(operands[0]));
                //case SetLineDashPattern.Symbol:
                //    return new SetLineDashPattern(TokensToDoubleArray(operands, true), OperandToInt(operands[operands.Count - 1]));
                //case SetLineJoin.Symbol:
                //    return new SetLineJoin(OperandToInt(operands[0]));
                //case SetLineWidth.Symbol:
                //    return new SetLineWidth(OperandToDouble(operands[0]));
                //case SetMiterLimit.Symbol:
                //    return new SetMiterLimit(OperandToDouble(operands[0]));
                //case AppendDualControlPointBezierCurve.Symbol:
                //    if (operands.Count == 0)
                //    {
                //        return null;
                //    }
                //    return new AppendDualControlPointBezierCurve(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]),
                //        OperandToDouble(operands[3]),
                //        OperandToDouble(operands[4]),
                //        OperandToDouble(operands[5]));
                //case AppendEndControlPointBezierCurve.Symbol:
                //    if (operands.Count == 0)
                //    {
                //        return null;
                //    }
                //    return new AppendEndControlPointBezierCurve(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]),
                //        OperandToDouble(operands[3]));
                //case AppendRectangle.Symbol:
                //    if (operands.Count == 0)
                //    {
                //        return null;
                //    }
                //    return new AppendRectangle(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]),
                //        OperandToDouble(operands[3]));
                //case AppendStartControlPointBezierCurve.Symbol:
                //    if (operands.Count == 0)
                //    {
                //        return null;
                //    }
                //    return new AppendStartControlPointBezierCurve(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]),
                //        OperandToDouble(operands[3]));
                //case AppendStraightLineSegment.Symbol:
                //    return new AppendStraightLineSegment(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]));
                //case BeginNewSubpath.Symbol:
                //    return new BeginNewSubpath(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]));
                //case CloseSubpath.Symbol:
                //    return CloseSubpath.Value;
                case ModifyCurrentTransformationMatrix.Symbol:
                    return new ModifyCurrentTransformationMatrix(TokensToDoubleArray(operands));
                case Pop.Symbol:
                    return Pop.Value;
                case Push.Symbol:
                    return Push.Value;
                case SetGraphicsStateParametersFromDictionary.Symbol:
                    return new SetGraphicsStateParametersFromDictionary((NameToken)operands[0]);
                case BeginText.Symbol:
                    return BeginText.Value;
                case EndText.Symbol:
                    return EndText.Value;
                case SetCharacterSpacing.Symbol:
                    return new SetCharacterSpacing(OperandToDouble(operands[0]));
                case SetFontAndSize.Symbol:
                    return new SetFontAndSize((NameToken)operands[0], OperandToDouble(operands[1]));
                case SetHorizontalScaling.Symbol:
                    return new SetHorizontalScaling(OperandToDouble(operands[0]));
                case SetTextLeading.Symbol:
                    return new SetTextLeading(OperandToDouble(operands[0]));
                case SetTextRenderingMode.Symbol:
                    return new SetTextRenderingMode(OperandToInt(operands[0]));
                case SetTextRise.Symbol:
                    return new SetTextRise(OperandToDouble(operands[0]));
                case SetWordSpacing.Symbol:
                    return new SetWordSpacing(OperandToDouble(operands[0]));
                //case CloseAndStrokePath.Symbol:
                //    return CloseAndStrokePath.Value;
                //case CloseFillPathEvenOddRuleAndStroke.Symbol:
                //    return CloseFillPathEvenOddRuleAndStroke.Value;
                //case CloseFillPathNonZeroWindingAndStroke.Symbol:
                //    return CloseFillPathNonZeroWindingAndStroke.Value;
                //case BeginInlineImage.Symbol:
                //    return BeginInlineImage.Value;
                /*
                case BeginMarkedContent.Symbol:
                    return new BeginMarkedContent((NameToken)operands[0]);
                case BeginMarkedContentWithProperties.Symbol:
                    var bdcName = (NameToken)operands[0];
                    var contentSequence = operands[1];

                    if (contentSequence is DictionaryToken contentSequenceDictionary)
                    {
                        return new BeginMarkedContentWithProperties(bdcName, contentSequenceDictionary);
                    }

                    if (contentSequence is NameToken contentSequenceName)
                    {
                        return new BeginMarkedContentWithProperties(bdcName, contentSequenceName);
                    }

                    var errorMessageBdc = string.Join(", ", operands.Select(x => x.ToString()));
                    throw new PdfDocumentFormatException($"Attempted to set a marked-content sequence with invalid parameters: [{errorMessageBdc}]");
                case DesignateMarkedContentPoint.Symbol:
                    return new DesignateMarkedContentPoint((NameToken)operands[0]);
                case DesignateMarkedContentPointWithProperties.Symbol:
                    var dpName = (NameToken)operands[0];
                    var contentPoint = operands[1];

                    if (contentPoint is DictionaryToken contentPointDictionary)
                    {
                        return new DesignateMarkedContentPointWithProperties(dpName, contentPointDictionary);
                    }

                    if (contentPoint is NameToken contentPointName)
                    {
                        return new DesignateMarkedContentPointWithProperties(dpName, contentPointName);
                    }

                    var errorMessageDp = string.Join(", ", operands.Select(x => x.ToString()));
                    throw new PdfDocumentFormatException($"Attempted to set a marked-content point with invalid parameters: [{errorMessageDp}]");
                case EndMarkedContent.Symbol:
                    return EndMarkedContent.Value;
                */
                //case EndPath.Symbol:
                //    return EndPath.Value;
                //case FillPathEvenOddRule.Symbol:
                //    return FillPathEvenOddRule.Value;
                //case FillPathEvenOddRuleAndStroke.Symbol:
                //    return FillPathEvenOddRuleAndStroke.Value;
                //case FillPathNonZeroWinding.Symbol:
                //    return FillPathNonZeroWinding.Value;
                //case FillPathNonZeroWindingAndStroke.Symbol:
                //    return FillPathNonZeroWindingAndStroke.Value;
                //case FillPathNonZeroWindingCompatibility.Symbol:
                //    return FillPathNonZeroWindingCompatibility.Value;
                case InvokeNamedXObject.Symbol:
                    return new InvokeNamedXObject((NameToken)operands[0]);
                case MoveToNextLine.Symbol:
                    return MoveToNextLine.Value;
                case MoveToNextLineShowText.Symbol:
                    {
                        if (operands.Count != 1)
                        {
                            throw new InvalidOperationException($"Attempted to create a move to next line and show text operation with {operands.Count} operands.");
                        }

                        var operand = operands[0];

                        if (operand is StringToken snl)
                        {
                            return new MoveToNextLineShowText(snl.Data);
                        }

                        if (operand is HexToken hnl)
                        {
                            return new MoveToNextLineShowText(hnl.Bytes.ToArray());
                        }

                        throw new InvalidOperationException($"Tried to create a move to next line and show text operation with operand type: {operands[0]?.GetType().Name ?? "null"}");
                    }
                case MoveToNextLineWithOffset.Symbol:
                    return new MoveToNextLineWithOffset(OperandToDouble(operands[0]), OperandToDouble(operands[1]));
                case MoveToNextLineWithOffsetSetLeading.Symbol:
                    return new MoveToNextLineWithOffsetSetLeading(OperandToDouble(operands[0]), OperandToDouble(operands[1]));
                case MoveToNextLineShowTextWithSpacing.Symbol:
                    {
                        var wordSpacing = (NumericToken)operands[0];
                        var charSpacing = (NumericToken)operands[1];
                        var text = operands[2];

                        if (text is StringToken stringToken)
                        {
                            return new MoveToNextLineShowTextWithSpacing(wordSpacing.Double, charSpacing.Double,
                                stringToken.Data);
                        }

                        if (text is HexToken hexToken)
                        {
                            return new MoveToNextLineShowTextWithSpacing(wordSpacing.Double, charSpacing.Double,
                                hexToken.Bytes.ToArray());
                        }

                        throw new InvalidOperationException($"Tried to create a MoveToNextLineShowTextWithSpacing operation with operand type: {operands[2]?.GetType().Name ?? "null"}");
                    }
                //case PaintShading.Symbol:
                //    return new PaintShading((NameToken)operands[0]);
                //case SetNonStrokeColor.Symbol:
                //    return new SetNonStrokeColor(TokensToDoubleArray(operands));
                //case SetNonStrokeColorAdvanced.Symbol:
                //    if (operands[operands.Count - 1] is NameToken scnLowerPatternName)
                //    {
                //        return new SetNonStrokeColorAdvanced(operands.Take(operands.Count - 1).Select(x => ((NumericToken)x).Data).ToArray(), scnLowerPatternName);
                //    }
                //    else if (operands.All(x => x is NumericToken))
                //    {
                //        return new SetNonStrokeColorAdvanced(operands.Select(x => ((NumericToken)x).Data).ToArray());
                //    }

                //    var errorMessageScnLower = string.Join(", ", operands.Select(x => x.ToString()));
                //    throw new PdfDocumentFormatException($"Attempted to set a non-stroke color space (scn) with invalid arguments: [{errorMessageScnLower}]");
                //case SetNonStrokeColorDeviceCmyk.Symbol:
                //    return new SetNonStrokeColorDeviceCmyk(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]),
                //        OperandToDouble(operands[3]));
                //case SetNonStrokeColorDeviceGray.Symbol:
                //    return new SetNonStrokeColorDeviceGray(OperandToDouble(operands[0]));
                //case SetNonStrokeColorDeviceRgb.Symbol:
                //    return new SetNonStrokeColorDeviceRgb(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]));
                //case SetNonStrokeColorSpace.Symbol:
                //    return new SetNonStrokeColorSpace((NameToken)operands[0]);
                //case SetStrokeColor.Symbol:
                //    return new SetStrokeColor(TokensToDoubleArray(operands));
                //case SetStrokeColorAdvanced.Symbol:
                //    if (operands[operands.Count - 1] is NameToken scnPatternName)
                //    {
                //        return new SetStrokeColorAdvanced(operands.Take(operands.Count - 1).Select(x => ((NumericToken)x).Data).ToArray(), scnPatternName);
                //    }
                //    else if (operands.All(x => x is NumericToken))
                //    {
                //        return new SetStrokeColorAdvanced(operands.Select(x => ((NumericToken)x).Data).ToArray());
                //    }

                //    var errorMessageScn = string.Join(", ", operands.Select(x => x.ToString()));
                //    throw new PdfDocumentFormatException($"Attempted to set a stroke color space (SCN) with invalid arguments: [{errorMessageScn}]");
                //case SetStrokeColorDeviceCmyk.Symbol:
                //    return new SetStrokeColorDeviceCmyk(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]),
                //        OperandToDouble(operands[3]));
                //case SetStrokeColorDeviceGray.Symbol:
                //    return new SetStrokeColorDeviceGray(OperandToDouble(operands[0]));
                //case SetStrokeColorDeviceRgb.Symbol:
                //    return new SetStrokeColorDeviceRgb(OperandToDouble(operands[0]),
                //        OperandToDouble(operands[1]),
                //        OperandToDouble(operands[2]));
                //case SetStrokeColorSpace.Symbol:
                //    return new SetStrokeColorSpace((NameToken)operands[0]);
                case SetTextMatrix.Symbol:
                    return new SetTextMatrix(TokensToDoubleArray(operands));
                //case StrokePath.Symbol:
                //    return StrokePath.Value;
                case ShowText.Symbol:
                    {
                        if (operands.Count != 1)
                        {
                            throw new InvalidOperationException($"Attempted to create a show text operation with {operands.Count} operands.");
                        }

                        var operand = operands[0];

                        if (operand is StringToken s)
                        {
                            return new ShowText(s.Data);
                        }

                        if (operand is HexToken h)
                        {
                            return new ShowText(h.Bytes.ToArray());
                        }

                        throw new InvalidOperationException($"Tried to create a show text operation with operand type: {operand?.GetType().Name ?? "null"}");
                    }
                case ShowTextsWithPositioning.Symbol:
                    if (operands.Count == 0)
                    {
                        throw new InvalidOperationException("Cannot have 0 parameters for a TJ operator.");
                    }

                    if (operands.Count == 1 && operands[0] is ArrayToken arrayToken)
                    {
                        return new ShowTextsWithPositioning(arrayToken.Data);
                    }

                    return new ShowTextsWithPositioning(operands);
            }

            return NoOpGraphicsStateOperation.Instance;
        }
    }
}
