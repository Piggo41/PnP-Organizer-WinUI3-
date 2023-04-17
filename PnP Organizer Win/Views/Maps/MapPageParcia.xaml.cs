﻿using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace PnPOrganizer.Views.Maps
{
    public sealed partial class MapPageParcia : Page
    {
        public MapPageParcia()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            anim?.TryStart(ContentGrid);
        }
    }
}
