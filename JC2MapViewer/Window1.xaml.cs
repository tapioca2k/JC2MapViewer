/*
 JC2MapViewer
 Copyright 2010 - DerPlaya78

 this program is free software: you can redistribute it and/or modify
 it under the terms of the GNU Lesser General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU Lesser General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BruTile;
using BruTile.Cache;
using BruTile.UI;
using BruTile.UI.Windows;
using JC2.Save;
using Microsoft.Win32;

namespace JC2MapViewer
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class Window1 : INotifyPropertyChanged
	{
		SaveFile _saveFile;
		bool _displaySettlements;
		DispatcherTimer _dispatcherTimer = new DispatcherTimer();
		FileSystemWatcher _fileSystemWatcher = new FileSystemWatcher() { NotifyFilter = NotifyFilters.LastWrite, Filter = "*.sav" };
		ManualResetEvent _refreshRequested = new ManualResetEvent(false);

		public Window1()
		{
			_dispatcherTimer.Tick += new EventHandler(_dispatcherTimer_Tick);

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			InitializeComponent();
			map.ErrorMessageChanged += map_ErrorMessageChanged;
			Loaded += Window1_Loaded;

			ChooserViewModel root = itemChooser.Items[0] as ChooserViewModel;

			CommandBindings.Add(
				new CommandBinding(
					ApplicationCommands.Undo,
					( sender, e ) => // Execute
					{
						e.Handled = true;
						root.IsChecked = false;
						itemChooser.Focus();
					},
					( sender, e ) => // CanExecute
					{
						e.Handled = true;
						e.CanExecute = ( root.IsChecked != false );
					} ) );

			itemChooser.Focus();

			string appdir = Path.GetDirectoryName( System.Reflection.Assembly.GetExecutingAssembly().Location );

			TileSource tileSource = new TileSource(
				new FileTileProvider( new FileCache( appdir + "\\Tiles", "jpg" ) ),
				new TileSchema()
			);

			map.RootLayer = new TileLayer( tileSource );

			_fileSystemWatcher.Changed += fileSystemWatcher_Changed;

			InitializeTransform( tileSource.Schema );
		}

		private void fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			for (int i = 0; i < 5; i++)
			{
				try
				{
					_saveFile = new SaveFile(e.FullPath);
					Dispatcher.Invoke(new Action(loadSavedInfo));
					return;
				}
				catch (IOException)
				{
					// the game is still updating the save file
					Thread.Sleep(TimeSpan.FromSeconds(i + 1));
				}
			}
		}

		void Window1_Loaded( object sender, RoutedEventArgs e )
		{
			map.Refresh();
		}

		private void _dispatcherTimer_Tick(object sender, EventArgs e)
		{
			ReloadButton_Click(sender, null);
		}

		void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
		{
			MessageBox.Show( "An unhandled exception occurred, the application will shut down", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error );
		}

		private void InitializeTransform( TileSchema schema )
		{
			map.Transform.Center = new Point( 16384d, -16384d );
			map.Transform.Resolution = schema.Resolutions.Last();
			schema.Resolutions.Add( 2 );
			schema.Resolutions.Add( 1 );
		}

		private void map_ErrorMessageChanged( object sender, EventArgs e )
		{
			Error.Text = map.ErrorMessage;
			Renderer.AnimateOpacity( errorBorder, 0.75, 0, 8000 );
		}

		private void AddIcon( Dictionary<int, ImageSource> list, int index, string name )
		{
			string appdir = Path.GetDirectoryName( System.Reflection.Assembly.GetExecutingAssembly().Location );

			BitmapImage bi = new BitmapImage();
			bi.BeginInit();
			string url = Path.Combine( Path.Combine( appdir, "Icons" ), string.Format( "{0}.png", name ) );
			bi.UriSource = new Uri( url );
			bi.EndInit();
			list.Add( index, bi );
		}

		private void loadMarkers( List<string> categories, List<Marker> list, List<string> iconNames, Dictionary<int, ImageSource> iconList, List<SavedObjectInfo> items, Dictionary<string, SavedSettlementInfo> settlements )
		{
			try
			{
				Dictionary<string, Dictionary<string, int>> missedPerSettlement = new Dictionary<string, Dictionary<string, int>>();

				Dictionary<string, List<Point>> itemPoints = new Dictionary<string, List<Point>>();
				foreach( var itemcollection in SavedObjectInfoLookup.Items )
				{
					foreach( var item in itemcollection.Value.Items )
					{
						if( item.Value.SettlementID == null )
							continue;
						if( !itemPoints.ContainsKey( item.Value.SettlementID ) )
							itemPoints.Add( item.Value.SettlementID, new List<Point>() );
						itemPoints[item.Value.SettlementID].Add( new Point( item.Value.PosX, item.Value.PosY ) );
					}
				}

				foreach( var item in items )
				{
					if( item.SettlementID != null )
					{
						if( !missedPerSettlement.ContainsKey( item.SettlementID ) )
							missedPerSettlement.Add( item.SettlementID, new Dictionary<string, int>() );
						if( !missedPerSettlement[item.SettlementID].ContainsKey( item.Name ) )
						{
							missedPerSettlement[item.SettlementID].Add( item.Name, 1 );
						}
						else
						{
							missedPerSettlement[item.SettlementID][item.Name] = missedPerSettlement[item.SettlementID][item.Name] + 1;
						}
					}

					if( !categories.Contains( item.Category ) )
						continue;

					int iconIndex = -1;

					if( !iconNames.Contains( item.IconName ) )
					{
						iconNames.Add( item.IconName );
						iconIndex = iconNames.IndexOf( item.IconName );
						AddIcon( iconList, iconIndex, item.IconName );
					}
					else
					{
						iconIndex = iconNames.IndexOf( item.IconName );
					}

					string text = string.Format( "{0}\nX: {1}\nY: {2}", item.Name, Math.Round( item.PosX ), Math.Round( item.PosY ) );
					string description = null;
					if( item.SettlementID != null )
					{
						if( settlements.ContainsKey( item.SettlementID ) )
						{
							description = string.Format( "\r\nPart of \"{0}\" ({1}%)", settlements[item.SettlementID].Text, settlements[item.SettlementID].PercentDone );
						}
					}

					Marker m = new Marker( item.PosX, item.PosY, true, iconIndex, text, description, 200 );
					list.Add( m );
				}

				if( _displaySettlements )
				{
					foreach( var s in settlements )
					{
						if( !s.Value.Completed )
						{
							string iconName = s.Value.Type.ToString();

							int iconIndex = -1;

							if( !iconNames.Contains( iconName ) )
							{
								iconNames.Add( iconName );
								iconIndex = iconNames.IndexOf( iconName );
								AddIcon( iconList, iconIndex, iconName );
							}
							else
							{
								iconIndex = iconNames.IndexOf( iconName );
							}

							string description = null;
							int totalchaos = 0;
							if( missedPerSettlement.ContainsKey( s.Key ) )
							{
								List<string> tmp = new List<string>();
								foreach( var i in missedPerSettlement[s.Key] )
								{
									int chaos = 0;
									if (ChaosPointInfo.points.ContainsKey(i.Key)) chaos = ChaosPointInfo.points[i.Key];
									else if (i.Key.StartsWith("Colonel ")) chaos = ChaosPointInfo.points["Colonel"]; // hack for unique colonel names
									totalchaos += chaos * i.Value;
									tmp.Add( string.Format( " {0}x  {1} {2}", i.Value, i.Key, chaos > 0 ? "(" + i.Value * chaos + "cp)" : "" ) );
								}
								description = string.Join( "\r\n", tmp.ToArray() );
							}

							string text = string.Format( "{0}\nCompleted: {1}%\nChaos Missing: {2}", s.Value.Text, s.Value.PercentDone, totalchaos );

							if( itemPoints.ContainsKey( s.Key ) )
							{
								Point[] pointcloud = itemPoints[s.Key].ToArray();
								Point origin = new Point();
								Convexhull.MinPoints( ref pointcloud, ref origin );
								Point[] convexPointcloud = Convexhull.ConvexHull( pointcloud );

								Marker m = new Marker( origin.X, origin.Y, true, iconIndex, text, description, 100 );
								m.Points = convexPointcloud;
								list.Add( m );

								m = new Marker( s.Value.X, s.Value.Y, true, iconIndex, text, description, 300 );
								list.Add( m );
							}
							else
							{
								Marker m = new Marker( s.Value.X, s.Value.Y, true, iconIndex, text, description, 300 );
								list.Add( m );
							}
						}
					}
				}
			}
			catch( Exception ex )
			{
				Error.Text = ex.Message;
				Renderer.AnimateOpacity( errorBorder, 0.75, 0, 8000 );
			}
		}

		private void loadSavedInfo()
		{
			ChooserViewModel root = itemChooser.Items[0] as ChooserViewModel;
			List<string> categories = root.GetSelectedCategories();

			map.ClearMarkers();
			map.MarkerImages.Clear();
			AddIcon( map.MarkerImages, -1, "noicon" );

			List<string> iconNames = new List<string>();

			try
			{
				Dictionary<string, SavedSettlementInfo> settlements = _saveFile.GetSettlementsInfo();

				Dictionary<string, int> counts;

				loadMarkers( categories, map.RootLayer.MarkerCache, iconNames, map.MarkerImages, _saveFile.GetSavedObjectInfo( out counts ), settlements );
				foreach( string c in counts.Keys )
				{
					root.UpdateCount( c, counts[c], SavedObjectInfoLookup.TotalCountByCategory[c] );
				}
			}
			catch( Exception ex )
			{
				Error.Text = ex.Message;
				Renderer.AnimateOpacity( errorBorder, 0.75, 0, 8000 );
				return;
			}
			map.Refresh();

		}

		private void LoadButton_Click( object sender, RoutedEventArgs e )
		{
			try
			{
				OpenFileDialog dlg = new OpenFileDialog();
				dlg.InitialDirectory = GetJC2SaveFolderPath() ?? "";
				dlg.Filter = "Just Cause 2 Save (.sav)|*.sav";
				Nullable<bool> result = dlg.ShowDialog();
				if( result == true )
				{
					_saveFile = new SaveFile( dlg.FileName );
					_fileSystemWatcher.Path = Path.GetDirectoryName(_saveFile.FileName);
					if (reloadInterval.SelectedIndex == 0)
					{
						_fileSystemWatcher.EnableRaisingEvents = true;
					}
					FirePropertyChanged( "SaveFileIsLoaded" );
					loadSavedInfo();
				}
			}
			catch( Exception ex )
			{
				Error.Text = ex.Message;
				Renderer.AnimateOpacity( errorBorder, 0.75, 0, 8000 );
			}
		}

		/// <summary>
		/// Determines the location of Just Cause 2 save files for the first Steam user on this computer.
		/// If there are multiple Steam users that play Just Cause 2, only the first folder will be returned.
		/// </summary>
		/// <returns>The Steam Cloud folder for Just Cause 2, or null, if it was not located</returns>
		private static string GetJC2SaveFolderPath()
		{
			try
			{
				// Find where Steam is installed. 64 bit versions of Windows will redirect it to the Wow6432Node location
				string installPath32 = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
				string installPath64 = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null) as string;
				string installPath = installPath32 ?? installPath64;
				if (installPath == null)
				{
					return null;
				}

				// Find the Steam user data folder
				string userDataPath = Path.Combine(installPath, "userdata");
				var userData = new DirectoryInfo(userDataPath);

				// Find the different Steam users accounts
				foreach (DirectoryInfo userFolder in userData.GetDirectories())
				{
					// Check if this Steam user has Just Cause 2 data
					DirectoryInfo jc2DataFolder = userFolder.GetDirectories("8190").FirstOrDefault();
					if (jc2DataFolder != null)
					{
						// Find the Steam Cloud folder, which stores the save games
						DirectoryInfo steamCloudFolder = jc2DataFolder.GetDirectories("remote").FirstOrDefault();
						if (steamCloudFolder != null)
						{
							return steamCloudFolder.FullName;
						}
					}
				}
			}
			catch
			{
				// Unable to determine location automatically
			}
			return null;
		}

		private void CheckBoxItem_Toggled( object sender, RoutedEventArgs e )
		{
			// Because multiple checkboxes might be toggled at the same time,
			// we request a refresh then attempt to perform it, if necessary.
			_refreshRequested.Set();
			ThreadPool.QueueUserWorkItem(RefreshIfNecessary);
		}

		/// <summary>
		/// Refreshes the map if a request is currently pending
		/// </summary>
		private void RefreshIfNecessary(object state)
		{
			lock (this)
			{
				if (!_refreshRequested.WaitOne(0))
				{
					// Refresh not necessary
					return;
				}

				if (_saveFile != null)
				{
					// Perform the refresh on the UI thread, so that we can update the map
					Action loadAction = loadSavedInfo;
					Dispatcher.Invoke(DispatcherPriority.Normal, loadAction);
				}

				// Clear the request
				_refreshRequested.Reset();
			}
		}

		private void ToggleSettlementsButton_Click( object sender, RoutedEventArgs e )
		{
			System.Windows.Controls.Primitives.ToggleButton tb = sender as System.Windows.Controls.Primitives.ToggleButton;
			if( tb.IsChecked.HasValue && tb.IsChecked.Value )
			{
				_displaySettlements = true;
			}
			else
			{
				_displaySettlements = false;
			}

			if( _saveFile != null )
			{
				loadSavedInfo();
			}
		}

		private void ReloadButton_Click( object sender, RoutedEventArgs e )
		{
			if( _saveFile != null )
			{
				_saveFile = new SaveFile( _saveFile.FileName );
				loadSavedInfo();
			}
		}

		private void ZoomInButton_Click( object sender, RoutedEventArgs e )
		{
			if( map.Transform.Resolution > 0.125 )
			{
				map.Transform.Resolution /= 2;
				map.Refresh();
			}
		}

		private void ZoomOutButton_Click( object sender, RoutedEventArgs e )
		{
			if( map.Transform.Resolution < 512 )
			{
				map.Transform.Resolution *= 2;
				map.Refresh();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;


		public bool SaveFileIsLoaded
		{
			get { return _saveFile != null; }
		}

		void FirePropertyChanged( string property )
		{
			if( PropertyChanged != null )
			{
				PropertyChanged( this, new PropertyChangedEventArgs( property ) );
			}
		}

		private void reloadInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			_fileSystemWatcher.EnableRaisingEvents = reloadInterval.SelectedIndex == 0 && SaveFileIsLoaded;

			switch(reloadInterval.SelectedIndex)
			{
				case 1:
					_dispatcherTimer.Interval = new TimeSpan(0, 1, 0);
					_dispatcherTimer.Start();
				break;

				case 2:
					_dispatcherTimer.Interval = new TimeSpan(0, 5, 0);
					_dispatcherTimer.Start();
				break;

				case 3:
					_dispatcherTimer.Interval = new TimeSpan(0, 10, 0);
					_dispatcherTimer.Start();
				break;

				case 4:
					_dispatcherTimer.Interval = new TimeSpan(0, 15, 0);
					_dispatcherTimer.Start();
				break;

				case 5:
					_dispatcherTimer.Stop();
				break;
			}
		}
	}
}
