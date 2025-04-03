using DrinkDb_Auth.Service;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace DrinkDb_Auth
{
    public sealed partial class UserPage : Page
    {
        public UserPage()
        {
            this.InitializeComponent();
            LoadUserData();
        }

        private void LoadUserData()
        {
            // Retrieve the current user's ID from the static property.
            Guid currentUserId = App.CurrentUserId;

            // Check if the user ID is valid (not empty)
            if (currentUserId != Guid.Empty)
            {
                // Retrieve the user from the database using your UserService.
                var userService = new UserService();
                var user = userService.GetUserById(currentUserId);

                // Update UI with the retrieved data.
                if (user != null)
                {
                    NameTextBlock.Text = user.Username; 
                    UsernameTextBlock.Text = "@" + user.Username;
                    StatusTextBlock.Text = "Status: Online";
                }
                else
                {
                    NameTextBlock.Text = "User not found";
                    UsernameTextBlock.Text = "";
                    StatusTextBlock.Text = "";
                }
            }
            else
            {
                // If no user is stored, show a default message.
                NameTextBlock.Text = "No user logged in";
                UsernameTextBlock.Text = "";
                StatusTextBlock.Text = "";
            }
        }


        private void LoadMockUserData()
        {
            // Create mock user data
            var mockUser = new UserModel
            {
                Name = "Serginio",
                Username = "sergio24",
                Status = "Online",
                Reviews = new List<ReviewModel>
                {
                    new ReviewModel
                    {
                        Date = DateTime.Now.AddDays(-2),
                        Rating = 4,
                        Comment = "Really good taste!"
                    },
                    new ReviewModel
                    {
                        Date = DateTime.Now.AddDays(-10),
                        Rating = 2,
                        Comment = "Could be better"
                    }
                },
                Drinklist = new List<string>
                {
                    "Coca Cola Zero",
                    "Pepsi Twist",
                    "Lemonade"
                }
            };

            // Show user info
            NameTextBlock.Text = mockUser.Name;
            UsernameTextBlock.Text = "@" + mockUser.Username;
            StatusTextBlock.Text = $"Status: {mockUser.Status}";

            // Display each review in the ReviewsItemsControl
            foreach (var review in mockUser.Reviews)
            {
                // Create a simple border "card"
                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(12)
                };

                // A small stack to hold rating, date, and comment
                var reviewStack = new StackPanel { Spacing = 4 };

                // Star rating
                string stars = new string('★', review.Rating) + new string('☆', 5 - review.Rating);
                var starsText = new TextBlock
                {
                    Text = stars,
                    FontSize = 20,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gold)
                };
                reviewStack.Children.Add(starsText);

                // Date
                var dateText = new TextBlock
                {
                    Text = review.Date.ToShortDateString(),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
                reviewStack.Children.Add(dateText);

                // Comment
                var commentText = new TextBlock
                {
                    Text = review.Comment,
                    FontSize = 14
                };
                reviewStack.Children.Add(commentText);

                border.Child = reviewStack;
                ReviewsItemsControl.Items.Add(border);
            }

            // Display each drink in the DrinklistItemsControl
            foreach (var drink in mockUser.Drinklist)
            {
                // Just display each drink as a TextBlock
                var drinkText = new TextBlock
                {
                    Text = drink,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                DrinklistItemsControl.Items.Add(drinkText);
            }
        }

        private async void EditAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Edit Account",
                Content = "Account editing is not implemented yet.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
            };

            await dialog.ShowAsync();
        }
    }

    // User model with default values for non-nullable properties
    public class UserModel
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<ReviewModel> Reviews { get; set; } = new List<ReviewModel>();
        public List<string> Drinklist { get; set; } = new List<string>();
    }

    public class ReviewModel
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public int Rating { get; set; } = 0;
        public string Comment { get; set; } = string.Empty;
    }
}
