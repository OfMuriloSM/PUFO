using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PUFO.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{

        private EstadoIlha _estadoIlha = EstadoIlha.Repouso;
        private void MudarEstado(EstadoIlha estado){
            _estadoIlha = estado;
            if (_estadoIlha == EstadoIlha.Repouso){
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = (0 - Height) / 1.8;
                Capsula.Height = 35;
                Capsula.Width= 150;
            }
            else if (_estadoIlha == EstadoIlha.Previa){
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = (0 - Height) / 4;
                Capsula.Height = 75;
                Capsula.Width= 150;
            }
            else if (_estadoIlha == EstadoIlha.Completo){
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = (0 - Height) / 10;
                Capsula.Height = 150;
                Capsula.Width= 300;
            }
        }
    public MainWindow()
    {
        InitializeComponent();
        MudarEstado(EstadoIlha.Repouso);
    }

}