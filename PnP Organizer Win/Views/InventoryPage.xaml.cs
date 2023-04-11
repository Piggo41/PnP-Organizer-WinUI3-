﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PnPOrganizer.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation.Metadata;
using PnPOrganizer.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI;
using PnPOrganizer.Views.Interfaces;
using System.Threading.Tasks;
using PnPOrganizer.Core;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;

namespace PnPOrganizer.Views
{
    [INotifyPropertyChanged]
    public sealed partial class InventoryPage : Page, IViewFor<InventoryPageViewModel>
    {
        public InventoryPageViewModel ViewModel { get; }

        [ObservableProperty]
        private InventoryItemViewModel? _storedItem;

        [ObservableProperty]
        private InventoryItemViewModel? _storedItemCopy;


        private const byte DETAILCARD_ALPHA = 224;

        public InventoryPage()
        {
            ViewModel = Ioc.Default.GetRequiredService<InventoryPageViewModel>();
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            SharedShadow.Receivers.Add(ItemsScrollViewer);
            SharedShadow.Receivers.Add(ItemsGridView);

            ViewModel.ItemModels?.Add(new InventoryItemViewModel()
            {
                Name = "Test Item",
                Description = "Short Description..."
            });
        }

        #region Item Detail View
        private async void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (StoredItemCopy != null)
            {
#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
                Utils.CopyProperties(StoredItemCopy, ref _storedItem!);
#pragma warning restore MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
            }
            await PlayTransitionAnimationAsync();
            ViewModel.ItemsView.Refresh();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            await PlayTransitionAnimationAsync();
        }

        private async Task PlayTransitionAnimationAsync()
        {
            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("backwardsAnimation", ItemDetails);
            SmokeGrid.Children.Remove(ItemDetails);

            animation.Completed += Animation_Completed;

            ItemsGridView.ScrollIntoView(StoredItem, ScrollIntoViewAlignment.Default);
            ItemsGridView.UpdateLayout();

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                animation.Configuration = new DirectConnectedAnimationConfiguration();
            }
            await ItemsGridView.TryStartConnectedAnimationAsync(animation, StoredItem, "Item");
        }

        private async void ItemImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();

            var hWnd = WindowHelper.GetCurrentProcMainWindowHandle();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".bmp");
            openPicker.FileTypeFilter.Add(".gif");

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var itemImage = new BitmapImage();

                using var stream = await file.OpenReadAsync();
                stream.Seek(0);
                await itemImage.SetSourceAsync(stream);

                if(StoredItemCopy != null)
                    StoredItemCopy.ItemImage = itemImage;
            }
        }

        private void Animation_Completed(ConnectedAnimation sender, object args)
        {
            SmokeGrid.Visibility = Visibility.Collapsed;
            SmokeGrid.Children.Add(ItemDetails);
        }
        #endregion

        #region ItemsGridView
        private void ItemsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ElementSoundPlayer.Play(ElementSoundKind.Focus);
            if (ItemsGridView.ContainerFromItem(e.ClickedItem) is GridViewItem container)
            {
                if(container.Content != null)
                {
                    StoredItem = (container.Content as InventoryItemViewModel)!;
                    StoredItemCopy = StoredItem.Copy();
                }
                var animation = ItemsGridView.PrepareConnectedAnimation("ForwardConnectedAnimation", StoredItem, "Item");
                
                if(StoredItem != null)
                {
                    ItemDetails.DataContext = StoredItem;

                    var translucentBrush = new SolidColorBrush(Color.FromArgb(DETAILCARD_ALPHA, StoredItem.Brush!.Color.R, StoredItem.Brush.Color.G, StoredItem.Brush.Color.B));

                    DetailHeaderGrid.Background = translucentBrush;
                    DetailHeaderName.Text = StoredItem.Name;
                    DetailContentGrid.Background = translucentBrush;
                    DetailContentDescr.Text = StoredItem.Description;
                }

                animation.TryStart(ItemDetails);
                SmokeGrid.Visibility = Visibility.Visible;
            }
        }

        // A bit hacky and doesn't look that great, but it's better than nothing
        private void ItemsGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) => ViewModel.ItemsView.Refresh();
        #endregion

        #region Edit Item Menu
        private void ShowMenu(bool isTransient, UIElement target)
        {
            var myOption = new FlyoutShowOptions
            {
                ShowMode = isTransient ? FlyoutShowMode.Transient : FlyoutShowMode.Standard
            };
            EditItemFlyout.ShowAt(target, myOption);
        }

        private void EditItemButton_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            ShowMenu(false, sender);
        }

        private void EditItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is UIElement target)
                ShowMenu(true, target);
        }
        #endregion

        #region Item Search
        private void SearchItemBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suitableItems = new List<InventoryItemViewModel>();
                var splitText = sender.Text.ToLower().Split(" ");
                foreach (var item in ViewModel.ItemModels)
                {
                    var found = splitText.All((key) =>
                    {
                        return item.Name.ToLower().Contains(key);
                    });
                    if (found)
                    {
                        suitableItems.Add(item);
                    }
                }
                if (suitableItems.Count == 0)
                {
                    //TODO Give information that no item was found
                }
                sender.ItemsSource = suitableItems.ConvertAll(invItem => invItem.Name);
            }
            FilterItems();
        }
        #endregion

        #region Filtering
        private void FilterItems()
        {
            if (ViewModel != null)
            {
                if (string.IsNullOrWhiteSpace(SearchItemBox.Text))
                {
                    ResetFilter();
                }
                else
                {
                    ResetFilter();
                    ViewModel.ItemsView.Filter = x => ((InventoryItemViewModel)x).Name.ToLower().Contains(SearchItemBox.Text.ToLower());
                }
            }
        }

        private void ResetFilter() => ViewModel.ItemsView.Filter = _ => true;
        #endregion

        #region Item Command Flyout
        private void CommandFlyoutColorButton_Click(object sender, RoutedEventArgs e) 
        {
            GetStoredItemFromButton((Button)sender);
            ItemColorPicker.Color = StoredItem!.Brush is not null ? StoredItem.Brush.Color : Color.FromArgb(255, 255, 255, 255);
        }

        private void CommandFlyoutClearButton_Click(object sender, RoutedEventArgs e)
        {
            GetStoredItemFromButton((Button)sender);
            if (StoredItem != null)
            {
                var tempNewItem = (InventoryItemViewModel?)Activator.CreateInstance(StoredItem.GetType());
                if(tempNewItem != null)
                {
#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
                    Utils.CopyProperties(tempNewItem, ref _storedItem!);
#pragma warning restore MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
                }
                ViewModel.ItemsView.Refresh();
            }
        }

        private void CommandFlyoutDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            GetStoredItemFromButton((Button)sender);
            if(StoredItem != null)
            {
                ViewModel.ItemModels?.Remove(StoredItem);
                StoredItem = null;
                StoredItemCopy = null;
            }
        }

        private void GetStoredItemFromButton(Button sender) => StoredItem = (InventoryItemViewModel)sender.DataContext;

        private void ConfirmColor_Click(object sender, RoutedEventArgs e)
        {
            if(StoredItem != null)
                StoredItem.Brush = new SolidColorBrush(ItemColorPicker.Color);

            ViewModel.ItemsView.Refresh();
            PickColorButton.Flyout.Hide();
        }

        private void CancelColor_Click(object sender, RoutedEventArgs e) => PickColorButton.Flyout.Hide();
        #endregion
    }
}
