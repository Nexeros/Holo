using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using HelixToolkit.Wpf;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;

namespace HoloTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                string dicomFolderPath = $"C:\\Users\\picas\\Desktop\\NEural\\STU00001\\SER0000{1}";
                double thresholdmin = -10777216;
                double thresholdmax = -1;
                (double[,,] volumeData, (double, double) spacing, double thickness) = VolumeProcessor.LoadVolume(dicomFolderPath, thresholdmin, thresholdmax);

                var modelGroup = VolumeRender.GenerateVolumeMesh(volumeData, thresholdmin, thresholdmax, spacing, thickness);

                var modelVisual = new ModelVisual3D { Content = modelGroup };
                viewport.Children.Add(modelVisual);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainWindow constructor: {ex.Message}");
            }
        }
    }




    class VolumeProcessor
    {
        public static (double[,,], (double, double), double) LoadVolume(string path, double thresholdmin, double thresholdmax)
        {
            var files = System.IO.Directory.GetFiles(path);
            files = files
                .Select(file =>
                {
                    var dicomFile = DicomFile.Open(file);
                    var instanceNumber = dicomFile.Dataset.GetString(DicomTag.InstanceNumber);
                    return new { FilePath = file, InstanceNumber = int.Parse(instanceNumber) };
                })
                .OrderBy(f => f.InstanceNumber)
                .Select(f => f.FilePath)
                .ToArray();

            (double, double) spacing = (1.00, 1.00);
            double thikness = 1.00;
            int width = 0;
            int height = 0;
            int depth = files.Length;
            List<double[]> slices = new List<double[]>();
            try
            {
                var dicomdatafile = DicomFile.Open(files[0]);
                //var dataset = dicomdatafile.Dataset;
                spacing = (
                    Convert.ToDouble(dicomdatafile.Dataset.GetString(DicomTag.PixelSpacing)[..5].Replace(".", ",")),
                    Convert.ToDouble(dicomdatafile.Dataset.GetString(DicomTag.PixelSpacing)[6..].Replace(".", ","))
                    );
                thikness = Convert.ToDouble(dicomdatafile.Dataset.GetString(DicomTag.SliceThickness).Replace(".", ","));

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }


            foreach (var file in files)
            {
                var dicomFile = DicomFile.Open(file);
                var image = new DicomImage(dicomFile.Dataset);
                if (width == 0 || height == 0)
                {
                    width = image.Width;
                    height = image.Height;
                }
                var pixels = image.RenderImage().Pixels;

                slices.Add(pixels.Data.Select(p => (double)p).ToArray());
            }
            //System.Diagnostics.Debug.WriteLine($"Width: {width}; Height: {height}; Depth: {depth}");
            double[,,] volume = new double[width, height, depth];
            Parallel.For(0, depth, z =>
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (slices[z][y * width + x] >= thresholdmin && slices[z][y * width + x] <= thresholdmax)
                        {
                            volume[x, y, z] = slices[z][y * width + x];
                        }
                    }
                }
            });
            //System.Diagnostics.Debug.WriteLine("------------");
            return (volume, spacing, thikness);
        }
        public static void Test(string path)
        {
            string dicomFilePath = path;
            var dicomFile = DicomFile.Open(dicomFilePath);
            var image = new DicomImage(dicomFile.Dataset);

            System.Diagnostics.Debug.WriteLine("------------");
            System.Diagnostics.Debug.WriteLine($"Width: {image.Width}; Height: {image.Height}");

            // Pobierz dane pikseli
            var pixels = image.RenderImage().Pixels;
            int count = (image.Height - 1) * image.Width + (image.Width - 1);
            int[] ipix = new int[count];
            for (int i = 0; i < count - 1; i++)
            {
                ipix[i] = pixels[i];
            }

            // Parametry histogramu
            int binSize = 10; // Rozmiar przedzia³u
            int minValue = ipix.Min(); // Minimalna wartoœæ w danych
            int maxValue = ipix.Max(); // Maksymalna wartoœæ w danych
            int numBins = (maxValue - minValue) / binSize + 1;

            // Tworzenie histogramu
            int[] histogram = new int[numBins];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {

                    int pixelValue = pixels[y * image.Width + x];
                    if (pixelValue > -16777216)
                    {
                        int binIndex = (pixelValue - minValue) / binSize;
                        histogram[binIndex]++;
                    }

                }
            }

            // ZnajdŸ przedzia³ z najwiêksz¹ liczb¹ pikseli
            int maxCount = 0;
            int maxBinIndex = 0;

            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] > maxCount)
                {
                    maxCount = histogram[i];
                    maxBinIndex = i;
                }
            }

            int rangeStart = minValue + maxBinIndex * binSize;
            int rangeEnd = rangeStart + binSize;

            System.Diagnostics.Debug.WriteLine($"Range with most pixels: {rangeStart} to {rangeEnd} (Count: {maxCount})");
        }

    }
    class VolumeRender
    {
        public static Model3DGroup GenerateVolumeMesh(double[,,] volumeData, double thresholdmin, double thresholdmax, (double, double) spacing, double thickness)
        {
            var modelGroup = new Model3DGroup();
            int width = volumeData.GetLength(0);
            int height = volumeData.GetLength(1);
            int depth = volumeData.GetLength(2);

            double minValue = volumeData.Cast<double>().Min();
            double maxValue = volumeData.Cast<double>().Max();

            var models = new ConcurrentBag<GeometryModel3D>();
            var testInfo = new Boolean[width];
            for (int i = 0; i < width; i++)
            {
                testInfo[i] = false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting Parallel.For with width = {width}");
                Parallel.For(0, width, x =>
                {
                    System.Diagnostics.Debug.WriteLine($"Starting processing for x = {x}");
                    testInfo[x] = true;
                    for (int y = 0; y < height; y++)
                    {
                        for (int z = 0; z < depth; z++)
                        {
                            try
                            {
                                double value = volumeData[x, y, z];
                                if (value >= thresholdmin && value <= thresholdmax)
                                {
                                    double normalizedValue = (value - minValue) / (maxValue - minValue);
                                    Color color = GetHeatMapColor(normalizedValue);

                                    var meshBuilder = new MeshBuilder();
                                    meshBuilder.AddBox(new Point3D(x * spacing.Item1, y * spacing.Item2, z * thickness), 1, 1, 1);
                                    var mesh = meshBuilder.ToMesh();

                                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        try
                                        {
                                            var material = new DiffuseMaterial(new SolidColorBrush(color));
                                            var model = new GeometryModel3D
                                            {
                                                Geometry = mesh,
                                                Material = material
                                            };

                                            models.Add(model);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Error in Dispatcher.BeginInvoke at ({x}, {y}, {z}): {ex.Message}");
                                        }
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error processing voxel at ({x}, {y}, {z}): {ex.Message}");
                            }
                        }
                    }
                    /*System.Diagnostics.Debug.WriteLine($"Completed processing for x = {x}");
                    testInfo[x] = false;
                    if (testInfo.Any(t => t == true))
                    {
                        System.Diagnostics.Debug.WriteLine($"not all threads completed for x = {x}");
                        for (var i = 0; i < width; i++)
                        {
                            if (testInfo[i] == true)
                            {
                                System.Diagnostics.Debug.WriteLine($"Thread {i} is still running");
                            }
                        }
                    }*/
                });
                //System.Diagnostics.Debug.WriteLine($"Ended Parallel.For with width = {width}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Parallel.For: {ex.Message}");
            }

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var model in models)
                    {
                        modelGroup.Children.Add(model);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding models to modelGroup: {ex.Message}");
            }

            return modelGroup;
        }


        private static Color GetHeatMapColor(double value)
        {
            try
            {
                const double minValue = 0.0;
                const double maxValue = 1.0;
                value = Math.Max(minValue, Math.Min(maxValue, value));

                double ratio = 2 * (value - minValue) / (maxValue - minValue);
                byte b = (byte)Math.Max(0, 255 * (1 - ratio));
                byte r = (byte)Math.Max(0, 255 * (ratio - 1));
                byte g = (byte)(255 - b - r);

                return Color.FromRgb(r, g, b);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetHeatMapColor: {ex.Message}");
                return Colors.Black;
            }
        }
    }





}