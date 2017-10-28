﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Locations;
using Android.Support.V7.App;
using Android.Support.V4.App;
using Newtonsoft.Json;

using Android.Content.PM;
using System.Threading.Tasks;
using Plugin.Connectivity;
using UberClone.Helpers;
using UberClone.Models;
using System.Net.Http;
using System.Collections.Specialized;

namespace UberClone.Activities
{
    [Activity(Label = "Current Location", ScreenOrientation = ScreenOrientation.Portrait)]
    public class ActivityYourLocation : FragmentActivity, ILocationListener, IOnMapReadyCallback
    {

        GoogleMap mMap; //Null if google apk services isn't available...
        LocationManager locationmanager;
        string provider;
       public Location location;
        CameraUpdate camera;
        string thisrequestdriverusername = null;
        Button button_requestuber, button_zoomin, button_zoomout;
        TextView tvinfo;

        Location mydriverlocation;

        bool requestactive = false;

        List<Marker> markers = new List<Marker>();
        LatLngBounds.Builder builder = new LatLngBounds.Builder();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Layout_ActivityYourLocation);
            button_requestuber = FindViewById<Button>(Resource.Id.button_requestuber);
            button_zoomin = FindViewById<Button>(Resource.Id.button_zoomin);
            button_zoomout = FindViewById<Button>(Resource.Id.button_zoomout);
            tvinfo = FindViewById<TextView>(Resource.Id.textviewinfo);

            button_requestuber.Click += button_requestuber_Click;
            button_zoomin.Click += Button_zoomin_Click;
            button_zoomout.Click += Button_zoomout_Click;

            SetUpMapIfNeeded();
        }

        private async void Button_zoomout_Click(object sender, EventArgs e)
        {
            var latlng = new LatLng(location.Latitude, location.Longitude);
            camera = CameraUpdateFactory.NewLatLng(latlng);
            mMap.MoveCamera(camera);
            await Task.Delay(500);
            camera = CameraUpdateFactory.ZoomOut();
            mMap.AnimateCamera(camera);
        }

        private async void Button_zoomin_Click(object sender, EventArgs e)
        {
            var latlng = new LatLng(location.Latitude, location.Longitude);
            camera = CameraUpdateFactory.NewLatLng(latlng);
            mMap.MoveCamera(camera);
            await Task.Delay(500);
            camera = CameraUpdateFactory.ZoomIn();
            mMap.AnimateCamera(camera);

        }

        private void SetUpMapIfNeeded()
        {
            // Do a null check to confirm that we have not already instantiated the map.
            if (mMap == null)
            {
                var frag = (SupportFragmentManager.FindFragmentById(Resource.Id.fragment_googlemap) as SupportMapFragment);
                frag.GetMapAsync(this);

            }
        }
        public void OnMapReady(GoogleMap googleMap)
        {
            this.mMap = googleMap;
            locationmanager = (LocationManager)GetSystemService(Context.LocationService);
            provider = locationmanager.GetBestProvider(new Criteria(), false);
            locationmanager.RequestLocationUpdates(provider, 400, 1, this);
            UpdateLocation();
        }
        private async void UpdateLocation()
        {
            mMap.Clear();
            location = locationmanager.GetLastKnownLocation(provider);
            
            

            if (requestactive == false)
            {
                var allrequest = await GetThisUserRequest();
                if (allrequest.Item1.Count>0)
                {
                    var userrequest = allrequest.Item1.Where(x => x.requester_username == Settings.Username).ToList<Request>();
                    if (userrequest.Count >0)
                    {
                        requestactive = true;
                        tvinfo.Text = "Finding UberDriver...";
                        button_requestuber.Text = "Cancel Uber";
                        thisrequestdriverusername = userrequest[0].driver_usename;
                        if (!string.IsNullOrEmpty(thisrequestdriverusername))
                        {
                            tvinfo.Text = "Your Driver Is Cumming";
                            button_requestuber.Visibility = ViewStates.Invisible;
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(thisrequestdriverusername))
            {
                if (location != null)
                {
                    LatLng latlng = new LatLng(location.Latitude, location.Longitude);
                    MarkerOptions options = new MarkerOptions().SetPosition(latlng).SetTitle("MyLocation");
                    mMap.AddMarker(options);
                    camera = CameraUpdateFactory.NewLatLngZoom(latlng, 18);
                    mMap.MoveCamera(camera);
                }
                else
                {
                    Toast.MakeText(this, "Locating...", ToastLength.Short);
                }
            }
            if (requestactive == true)
            {
                if (!String.IsNullOrEmpty(thisrequestdriverusername))
                {
                    var result = await GetThisUsersRequestDriverLocation();
                    if (result.Item1 != default(User) && result.Item1 != null)
                    {
                        mydriverlocation.Longitude = (double)result.Item1.user_longitude;
                        mydriverlocation.Latitude = (double)result.Item1.user_latitude;
                    }


                    if (mydriverlocation.Longitude != 0 && mydriverlocation.Latitude != 0)
                    {
                        var distanceinkm = GeoDistanceHelper.DistanceBetweenPlaces(location.Longitude, location.Latitude, mydriverlocation.Longitude, mydriverlocation.Latitude);
                        tvinfo.Text = "Your Driver is " + distanceinkm + " Km Away";

                        markers.Add(mMap.AddMarker(new MarkerOptions()
                           .SetPosition(new LatLng(location.Latitude, location.Longitude))
                           .SetTitle("MyLocation")));

                        markers.Add(mMap.AddMarker(new MarkerOptions()
                           .SetIcon(BitmapDescriptorFactory
                           .DefaultMarker(BitmapDescriptorFactory.HueBlue))
                           .SetPosition(new LatLng(mydriverlocation.Latitude, mydriverlocation.Longitude))
                           .SetTitle("RiderLocation")));
                        if (markers.Count > 0)
                        {
                            foreach (var m in markers)
                            {
                                builder.Include(m.Position);
                            }

                        }
                        mMap.MoveCamera(CameraUpdateFactory.NewLatLngBounds(builder.Build(), 100));
                    }
                }
                var result_update = await UpdateUserRequestLocationInDB();
                if (result_update.Item1)
                {
                    Toast.MakeText(this, result_update.Item2, ToastLength.Short);
                }
                if (!result_update.Item1)
                {
                    Toast.MakeText(this, result_update.Item2, ToastLength.Short);
                }

            }

           


        }
       
        private async void button_requestuber_Click(object sender, EventArgs e)
        {
            if (!requestactive)
            {
                /*create request save to db*/
                var saveresult = await SaveUsersRequest();
                requestactive = true;
                tvinfo.Text = "Finding UberDriver...";
                button_requestuber.Text = "Cancel Uber";
                
                
            }
            else
            {   
                /*remove request from db*/
                var saveresult = await DeleteUserRequest();
                requestactive = false;
                tvinfo.Text = "";
                button_requestuber.Text = "Request Uber";
                
            }
        }

        #region SaveRequestToDB
        private async Task<Tuple<bool, string>> SaveUsersRequest()
        {
            //check internet first
            if (CrossConnectivity.Current.IsConnected)
            {
                //internet available, setting up locals & save 'em to db

                var requestparameters = new FormUrlEncodedContent(new[]
               {
                     new KeyValuePair<string, string>("requester_username", Settings.Username),
                     new KeyValuePair<string, string>("user_longitude", location.Longitude.ToString()),
                     new KeyValuePair<string, string>("user_latitude", location.Latitude.ToString())
                 });
                var result = await RestHelper.APIRequest<Request>(AppUrls.api_url_requests, HttpVerbs.POST, null, requestparameters);
                if (result.Item1 != null & result.Item2)
                {
                    Settings.Request_ID = result.Item1.request_id.ToString();
                    return new Tuple<bool, string>(result.Item2, result.Item3);
                }
                else
                {
                    return new Tuple<bool, string>(result.Item2, result.Item3);
                }
            }
            else
            {
                //internet not available, user tries again later
                return new Tuple<bool, string>(false, "No Internet Connection!");
            }
        }
        #endregion

        #region GetUsersRequestFromDB

        private async Task<Tuple<List<Request>, bool, string>> GetThisUserRequest()
        {
            //check internet first
            if (CrossConnectivity.Current.IsConnected)
            {
                //internet available, setting up locals & getting this user's very own request

                var requestparameters = new NameValueCollection();
              requestparameters.Add("requester_username",Settings.Username);
                
                var result = await RestHelper.APIRequest<List<Request>>(AppUrls.api_url_requests, HttpVerbs.GET,requestparameters,null);
                if (result.Item1 != null & result.Item2)
                {
                    return new Tuple<List<Request>, bool, string>(result.Item1,result.Item2, result.Item3);
                }
                else
                {
                    return new Tuple<List<Request>, bool, string>(result.Item1, result.Item2, result.Item3);
                }
            }
            else
            {
                //internet not available, user tries again later
                return new Tuple<List<Request>, bool, string>(default(List<Request>), false, "No Internet Connection!");
            }
        }

        #endregion

        #region UpdateUserLocationInDB

        private async Task<Tuple<bool, string>> UpdateUserRequestLocationInDB()
        {
            //check internet first
            if (CrossConnectivity.Current.IsConnected)
            {
                //internet available, setting up locals & save 'em to db

                var requestparameters = new FormUrlEncodedContent(new[]
               {
                    new KeyValuePair<string, string>("requester_username",Settings.Username),
                     new KeyValuePair<string, string>("requester_longitude", location.Longitude.ToString()),
                     new KeyValuePair<string, string>("requester_latitude", location.Latitude.ToString())
                 });
                var result = await RestHelper.APIRequest<Request>(AppUrls.api_url_requests+Settings.User_ID, HttpVerbs.PUT, null,null, requestparameters);
                if (result.Item2)
                {
                    return new Tuple<bool, string>(result.Item2, result.Item3);
                }
                else
                {
                    return new Tuple<bool, string>(result.Item2, result.Item3);
                }
            }
            else
            {
                //internet not available, user tries again later
                return new Tuple<bool, string>(false, "No Internet Connection, Cannot Sync Your Location With Our Database");
            }
        }

        #endregion

        #region DeleteRequestFromDB&ClearLocals
        private async Task<bool> DeleteUserRequest()
        {
            if (!string.IsNullOrEmpty(Settings.Request_ID))
            {
                if (CrossConnectivity.Current.IsConnected)
                {
                    //attempting user deletion from db
                    string url = AppUrls.api_url_requests + Settings.Request_ID;
                    var httpClient = new HttpClient();
                    var response = await httpClient.DeleteAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        //successful attempt, cleaning local variables as well
                        Settings.ClearRequestLocalVars();
                        Toast.MakeText(this, "RequestDeleted", ToastLength.Short).Show();
                        return true;
                    }
                    else
                    {
                        //failed attempt, app stays open for now
                        Android.App.AlertDialog.Builder dialog = new Android.App.AlertDialog.Builder(this);
                        Android.App.AlertDialog alert = dialog.Create();
                        alert.SetTitle("Information!");
                        alert.SetMessage("Error: Couldn't Clean Request From Database!");
                        alert.SetIcon(Resource.Drawable.alert);
                        alert.SetButton("OK", (c, ev) =>
                        {

                        });
                        alert.Show();
                        return false;
                    }
                }
                else
                {
                    Toast.MakeText(this, "No Connection: Couldn't Clean Request From Database!", ToastLength.Long).Show();
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region GetDriverLocation

        private async Task<Tuple<User, bool, string>> GetThisUsersRequestDriverLocation()
        {
            //check internet first
            if (CrossConnectivity.Current.IsConnected)
            {
                //internet available, setting up locals & getting this user's very own request

                var requestparameters = new NameValueCollection();
                requestparameters.Add("username", thisrequestdriverusername);

                var result = await RestHelper.APIRequest<User>(AppUrls.api_url_users, HttpVerbs.GET, requestparameters, null);
                if (result.Item1 != null & result.Item2)
                {
                    return new Tuple<User, bool, string>(result.Item1, result.Item2, result.Item3);
                }
                else
                {
                    return new Tuple<User, bool, string>(result.Item1, result.Item2, result.Item3);
                }
            }
            else
            {
                //internet not available, user tries again later
                return new Tuple<User, bool, string>(default(User), false, "No Internet Connection!");
            }
        }

        #endregion

        public void OnLocationChanged(Location location)
        {
            UpdateLocation();
            Android.Util.Log.Info("UberCloneApp", "Location Changed");

        }
        public void OnProviderDisabled(string provider)
        {
            Android.Util.Log.Info("UberCloneApp", "Provider Disabled");
        }
        public void OnProviderEnabled(string provider)
        {
            Android.Util.Log.Info("UberCloneApp", "Provider Enabled");
        }
        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras)
        {
            Android.Util.Log.Info("UberCloneApp", "Status Changed");
        }
    }
}