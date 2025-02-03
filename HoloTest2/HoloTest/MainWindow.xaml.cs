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
            try {
                string dicomFolderPath = $"C:\\Users\\picas\\Desktop\\NEural\\STU00001\\SER0000{1}";
                double thresholdmin = -10777216;
                double thresholdmax = -1;
                (double[,,] volumeData, double spacing, double thikness) = VolumeProcessor.LoadVolume(dicomFolderPath, thresholdmin, thresholdmax);
                //System.Diagnostics.Debug.WriteLine("------------");
                //System.Diagnostics.Debug.WriteLine($"Min: {volumeData.Cast<double>().Min()}, Max: {volumeData.Cast<double>().Max()}");
                //System.Diagnostics.Debug.WriteLine("------------");
                var mesh = VolumeRender.GenerateVolumeMesh(volumeData, thresholdmin, thresholdmax, spacing, thikness);
                var model = new GeometryModel3D
                {
                    Geometry = mesh,
                    Material = MaterialHelper.CreateMaterial(System.Windows.Media.Colors.Red)
                };
                viewport.Children.Add(new ModelVisual3D { Content = model });
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }

    class VolumeProcessor
    {
        public static (double[,,], double, double) LoadVolume(string path, double thresholdmin, double thresholdmax)
        {
            var files = System.IO.Directory.GetFiles(path);
            System.Diagnostics.Debug.WriteLine($"File0: {files[0]}");
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
            double spacing = 1.00;
            double thikness = 1.00;
            int width = 0;
            int height = 0;
            int depth = files.Length;
            List<double[,]> slices = new List<double[,]>();
            try
            {
                var dicomdatafile = DicomFile.Open(files[0]);
                var dataset = dicomdatafile.Dataset;
                spacing = Convert.ToDouble(dicomdatafile.Dataset.GetString(DicomTag.PixelSpacing).Substring(0, 5).Replace(".", ","));
                thikness = Convert.ToDouble(dicomdatafile.Dataset.GetString(DicomTag.SliceThickness).Replace(".", ","));
                foreach (var item in dataset)
                {
                    try
                    {
                        //Slice Thickness, Value: 0.5 (mm)
                        //Image Position (Patient), Value: -112.910
                        //Image Orientation (Patient), Value: 1.00000
                        //Pixel Spacing, Value: 0.429
                        //
                        System.Diagnostics.Debug.WriteLine($"Tag: {item.Tag}, Name: {item.Tag.DictionaryEntry.Name}, Value: {dataset.GetValueOrDefault(item.Tag, 0, string.Empty)}");
                    }
                    catch(Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"Error: {ex}");
                    }
                }
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
                double[,] slice = new double[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        //optymalizuj te coś
                        slice[x, y] = pixels[y * width + x];
                        
                    }
                }
                slices.Add(slice);
            }
            System.Diagnostics.Debug.WriteLine($"Width: {width}; Height: {height}; Depth: {depth}");
            double[,,] volume = new double[width, height, depth];
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y+=5)
                {
                    for (int x = 0; x < width; x+=5)
                    {
                        if (slices[z][y, x] >= thresholdmin && slices[z][y, x] <= thresholdmax) 
                        {
                            volume[x, y, z] = slices[z][y, x];
                        }
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("------------");
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
            int count = (image.Height-1)*image.Width+(image.Width -1);
            int[] ipix = new int[count];
            for(int i = 0 ; i < count-1; i++)
            {
                ipix[i] = pixels[i];
            }

            // Parametry histogramu
            int binSize = 10; // Rozmiar przedziału
            int minValue = ipix.Min(); // Minimalna wartość w danych
            int maxValue = ipix.Max(); // Maksymalna wartość w danych
            int numBins = (maxValue - minValue) / binSize + 1;

            // Tworzenie histogramu
            int[] histogram = new int[numBins];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {

                    int pixelValue = pixels[y * image.Width + x];
                    if(pixelValue > -16777216) {
                        int binIndex = (pixelValue - minValue) / binSize;
                        histogram[binIndex]++; 
                    }
                    
                }
            }

            // Znajdź przedział z największą liczbą pikseli
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
        public static MeshGeometry3D GenerateVolumeMesh(double[,,] volumeData, double thresholdmin, double thresholdmax, double spacing, double thikness)
        {
            var meshBuilder = new MeshBuilder();
            int width = volumeData.GetLength(0);
            int height = volumeData.GetLength(1);
            int depth = volumeData.GetLength(2);
            System.Diagnostics.Debug.WriteLine($"Min: {volumeData.Cast<double>().Min()}, Max: {volumeData.Cast<double>().Max()}");
            //Pixel Spacing, Value: 0.429
            //Slice Thickness, Value: 0.5 (mm)
            for (int x = 0; x < width; x+=1)
            {
                for(int y = 0;y < height; y+=1)
                {
                    for(int z = 0; z < depth; z ++)
                    {
                        if (volumeData[x, y, z] >= thresholdmin && volumeData[x, y, z] <= thresholdmax)
                        {
                            meshBuilder.AddBox(new Point3D(x*spacing,y*spacing,z*thikness), 1, 1, 1);
                        }
                        //break;
                    }
                }
                
            }
            return meshBuilder.ToMesh();
        }
    }
}