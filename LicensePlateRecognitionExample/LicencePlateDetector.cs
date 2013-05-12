using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;
using AForge.Neuro;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicensePlateRecognition
{
    class LicensePlateDetector
    {

        private int _imageWidth = 116;
        private int _imageHeight = 26;
        private int _windowWidth = 17;
        private int _windowHeight = 26;
        private int _thresholdLevel = 120;

        private int[] _rounds = new int[] { 2, 3, 5 };

        public bool saveImages = false;

        private ActivationNetwork _net;
        private ActivationNetwork _netLetters;

        public LicensePlateDetector(ActivationNetwork net, ActivationNetwork netLetters)
        {
            _net = net;
            _netLetters = netLetters;
        }

        public string Detect(Bitmap image)
        {
            TextDetector result = new TextDetector();
            List<TextItem> detectionRound;

            foreach (int i in _rounds)
            {
                detectionRound = this.DetectText(image, i);
                result.AddList(detectionRound);
            }

            detectionRound = this.DetectText(image, 3, true);
            result.AddList(detectionRound, true);

            return result.GetText();
        }

        public List<TextItem> DetectText(Bitmap image, int shift = 3, bool onlyLetters = false)
        {
            List<TextItem> detectedContent = new List<TextItem>();

            int x = 0;
            TextItem item;
            while (x + _windowWidth < image.Width)
            {
                Rectangle rectangle = new Rectangle(x, 0, _windowWidth, _windowHeight);
                Bitmap cut = image.Clone(rectangle, System.Drawing.Imaging.PixelFormat.DontCare);
                Bitmap newImage = new Bitmap(_windowWidth, _windowHeight);

                using (var graphics = Graphics.FromImage(newImage))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(cut, new Rectangle(0, 0, _windowWidth, _windowHeight));
                }

                x += shift;

                double[] input = ImageArray.FromBitmap(newImage, true);
                double[] result = (!onlyLetters) ? _net.Compute(input) : _netLetters.Compute(input);

                double max = double.MinValue;
                int index = 0;
                int maxIndex = 0;
                foreach (double val in result)
                {
                    if (val > max)
                    {
                        max = val;
                        maxIndex = index;
                    }
                    index++;
                }

                if (max >= 0.3)
                {
                    item = new TextItem();
                    item.Position = x;
                    item.PositionEnd = x + _windowHeight;
                    item.Text = !onlyLetters ? maxIndex.ToString()[0] : this.numberToChar(maxIndex);
                    item.Accuracy = max;
                    detectedContent.Add(item);

                    if (saveImages)
                    {
                        newImage.Save("test3/imgb" + x + "f" + maxIndex.ToString() + "----" + Math.Round(max, 3).ToString() + ".png");
                    }
                }
            }

            return detectedContent;
        }

        private char numberToChar(int num)
        {
            char x = '-';

            switch (num)
            {
                case 0: x = 'A'; break;
                case 1: x = 'B'; break;
                case 2: x = 'E'; break;
                case 3: x = 'H'; break;
                case 4: x = 'J'; break;
                case 5: x = 'K'; break;
                case 6: x = 'M'; break;
                case 7: x = 'N'; break;
                case 8: x = 'P'; break;
                case 9: x = 'S'; break;
                case 10: x = 'T'; break;
                case 11: x = 'U'; break;
                case 12: x = 'Z'; break;
            }

            return x;
        }

        public List<string> DetectLicensePlate(Bitmap bitmap)
        {
            Bitmap original = (Bitmap)bitmap.Clone();

            // threshold obrazku - s timto lze experimentovat a zlepsit detekci znacek v nekterych obraycich
            bitmap = Grayscale.CommonAlgorithms.BT709.Apply(bitmap);
            OtsuThreshold threshold = new OtsuThreshold();
            bitmap = threshold.Apply(bitmap);

            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

            // vyhledani objektu
            BlobCounter blobCounter = new BlobCounter();

            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 10;
            blobCounter.MinWidth = 60;

            blobCounter.ProcessImage(bitmapData);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            bitmap.UnlockBits(bitmapData);

            // detekce objektu a jeho tvaru
            SimpleShapeChecker shapeChecker = new SimpleShapeChecker();
            //shapeChecker.AngleError = 15;
            //shapeChecker.MinAcceptableDistortion = 1;
            //shapeChecker.RelativeDistortionLimit = 0.05f;

            List<string> results = new List<string>();

            int a = 0;
            string text;
            for (int i = 0, n = blobs.Length; i < n; i++)
            {
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[i]);

                List<IntPoint> corners;
                if (shapeChecker.IsConvexPolygon(edgePoints, out corners))
                {
                    // zjisteni typu (obdelnik, ...)
                    PolygonSubType subType = shapeChecker.CheckPolygonSubType(corners);

                    if (corners.Count() == 4)
                    {
                        Bitmap rectangle = this.Transform(corners, original);
                        text = this.Detect(rectangle);
                        if (text.Length > 6)
                        {
                            results.Add(text);
                            rectangle.Save("temp/" + text + ".jpg");
                        }
                    }
                }
            }

            return results;
        }

        private Bitmap Transform(List<IntPoint> corners, Bitmap image)
        {
            // otestovat zamenu za SimpleQuadrilateralTransformation - mela by byt rychlejsi
            QuadrilateralTransformation filter = new QuadrilateralTransformation(corners);

            Bitmap newImage = filter.Apply(image);

            // zpetna rotace kvuli transformaci do puvodniho tvaru
            if (newImage.Height > newImage.Width)
            {
                newImage.RotateFlip(RotateFlipType.Rotate270FlipNone);
            }

            newImage = this.Resize(newImage);

            return this.Threshold(newImage, _thresholdLevel);
        }

        private Bitmap Resize(Bitmap srcImage)
        {
            var newImage = new Bitmap(_imageWidth, _imageHeight);

            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(srcImage, new Rectangle(0, 0, _imageWidth, _imageHeight));
            }

            return newImage;
        }

        private Bitmap Threshold(Bitmap original, int value = 100)
        {
            Grayscale filter = new Grayscale(0.2125, 0.7154, 0.0721);
            Bitmap image = filter.Apply(original);
            Threshold filtera = new Threshold(value);

            return filtera.Apply(image);
        }

    }


    class TextItem
    {

        public int Position;
        public int PositionEnd;
        public char Text;
        public double Accuracy;

    }

    class TextDetector
    {

        private TextItem[] _text = new TextItem[7];

        public void Add(TextItem item, bool isLetter = false)
        {
            int index = -1;
            if (item.Position >= 0 && item.Position <= 18)
                index = 0;

            if (item.Position >= 14 && item.Position <= 32 && isLetter)
                index = 1;

            if (item.Position >= 27 && item.Position <= 43)
                index = 2;

            if (item.Position >= 55 && item.Position <= 64)
                index = 3;

            if (item.Position >= 70 && item.Position <= 77) // 86
                index = 4;

            if (item.Position >= 82 && item.Position <= 91)
                index = 5;

            if (item.Position >= 96 && item.Position <= 116)
                index = 6;

            if (index >= 0 && ((isLetter && (index == 1 || index == 2)) || !isLetter))
            {
                if (_text[index] != null)
                {
                    if (item.Accuracy >= _text[index].Accuracy)
                    {
                        _text[index] = item;
                    }
                }
                else
                {
                    _text[index] = item;
                }
            }

        }

        public void AddList(List<TextItem> list, bool isLetter = false)
        {
            foreach (TextItem item in list)
            {
                if (item != null)
                {
                    this.Add(item, isLetter);
                }
            }
        }

        public string GetText()
        {
            string text = "";
            foreach (TextItem item in _text)
            {
                if (item != null)
                {
                    text += item.Text;
                }
            }

            return text;
        }

        public static TextDetector FromList(List<TextItem> list)
        {
            TextDetector detector = new TextDetector();

            foreach (TextItem item in list)
            {
                if (item != null)
                {
                    detector.Add(item);
                }
            }

            return detector;
        }

    }

    class ImageArray
    {

        public static double[] FromBitmap(Bitmap image, bool threshold = false)
        {
            double[] matrix = new double[image.Width * image.Height];

            int i = 0;
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    if (!threshold)
                    {
                        matrix[i] = image.GetPixel(x, y).B;
                    }
                    else
                    {
                        matrix[i] = image.GetPixel(x, y).R == 255 ? 0.5 : -0.5;
                    }
                    i++;
                }
            }

            return matrix;
        }

    }

}