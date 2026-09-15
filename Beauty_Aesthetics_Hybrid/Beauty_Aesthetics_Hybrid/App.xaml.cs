namespace Beauty_Aesthetics_Hybrid
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "Beauty_Aesthetics_Hybrid" };
        }
    }
}
