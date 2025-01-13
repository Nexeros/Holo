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
            System.IO.File.AppendAllText("log.txt", "Twoja wiadomość\n");
            System.Diagnostics.Debug.WriteLine("Twoja wiadomość debugowania");
            string dicomFolderPath = "C:\\Users\\damian\\Desktop\\NEural\\STU00001\\SER00001";
            System.Console.WriteLine("------------");
            double[,,] volumeData = VolumeProcessor.LoadVolume(dicomFolderPath);
            System.Console.WriteLine("------------");
            Console.WriteLine($"Min: {volumeData.Cast<double>().Min()}, Max: {volumeData.Cast<double>().Max()}");
            System.Console.WriteLine("------------");
            double threshold = -500;
            var mesh = VolumeRender.GenerateVolumeMesh(volumeData, threshold);
            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = MaterialHelper.CreateMaterial(System.Windows.Media.Colors.Red)
            };
            viewport.Children.Add(new ModelVisual3D { Content = model });
        }
        private static readonly string logFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "HoloTestLogs",
        "log.txt"
    );
    }

    class VolumeProcessor
    {
        public static double[,,] LoadVolume(string path)
        {
            var files = System.IO.Directory.GetFiles(path);
            Array.Sort(files);
            int width = 0;
            int height = 0;
            int depth = files.Length;
            List<double[,]> slices = new List<double[,]>();
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
                        slice[x, y] = pixels[y * width + x];
                    }
                }
                slices.Add(slice);
            }
            double[,,] volume = new double[width, height, depth];
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        volume[x, y, z] = slices[z][y, x];
                    }
                }
            }
            return volume;
        }
        static void Test()
        {
            string dicomFilePath = "C:\\Users\\damian\\Desktop\\NEural\\STU00001\\SER00001\\IMG00001";
            var dicomFile = DicomFile.Open(dicomFilePath);
            var image = new DicomImage(dicomFile.Dataset);
            Console.WriteLine(image.Width);
        }
    }
    class VolumeRender
    {
        public static MeshGeometry3D GenerateVolumeMesh(double[,,] volumeData, double threshold)
        {
            var meshBuilder = new MeshBuilder();
            int width = volumeData.GetLength(0);
            int height = volumeData.GetLength(1);
            int depth = volumeData.GetLength(2);

            for(int x = 0; x < width; x++)
            {
                for(int y = 0;y < height; y++)
                {
                    for(int z = 0; z < depth; z++)
                    {
                        if(volumeData[x,y,z] > threshold)
                        {
                            meshBuilder.AddBox(new Point3D(x,y,z), 1, 1, 1);
                        }
                    }
                }
            }
            return meshBuilder.ToMesh();
        }
    }
}