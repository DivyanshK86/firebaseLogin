using Firebase;
using Firebase.Database;
using Firebase.Unity.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class SignInAuthenticator : MonoBehaviour {

    public Text LoggerText;

    string WEB_CLIENT_ID = "129044841185-5s9p870updhpblbn4331e9g0reubdp1c.apps.googleusercontent.com";
    string User_Email;
    string User_Password = "abcd@12345";





    protected Firebase.Auth.FirebaseAuth auth;
    protected Firebase.Auth.FirebaseAuth otherAuth;
    protected Dictionary<string, Firebase.Auth.FirebaseUser> userByAuth =
        new Dictionary<string, Firebase.Auth.FirebaseUser>();

    protected string displayName = "";
    protected string phoneNumber = "";
    protected string receivedCode = "";
    // Whether to sign in / link or reauthentication *and* fetch user profile data.
    protected bool signInAndFetchProfile = false;
    // Flag set when a token is being fetched.  This is used to avoid printing the token
    // in IdTokenChanged() when the user presses the get token button.
    private bool fetchingToken = false;
    // Enable / disable password input box.
    // NOTE: In some versions of Unity the password input box does not work in
    // iOS simulators.
    private Vector2 controlsScrollViewVector = Vector2.zero;

    // Set the phone authentication timeout to a minute.
    // The verification id needed along with the sent code for phone authentication.
    private string phoneAuthVerificationId;

    // Options used to setup secondary authentication object.
    private Firebase.AppOptions otherAuthOptions = new Firebase.AppOptions { ApiKey = "", AppId = "", ProjectId = ""};

    const int kMaxLogSize = 16382;
    Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    ArrayList leaderBoard = new ArrayList();

    private const int MaxScores = 5;
    private int score = 100;

    public virtual void Start() {
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available) {
                InitializeFirebase();
            } else {

            }
        });
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            Application.Quit();
        }
    }

    public void DebugLog(string s) {
        Debug.Log(s);
    }

    // Handle initialization of the necessary firebase modules:
    protected void InitializeFirebase() {
        auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        auth.IdTokenChanged += IdTokenChanged;
        // Specify valid options to construct a secondary authentication object.
        if (otherAuthOptions != null &&
            !(String.IsNullOrEmpty(otherAuthOptions.ApiKey) ||
                String.IsNullOrEmpty(otherAuthOptions.AppId) ||
                String.IsNullOrEmpty(otherAuthOptions.ProjectId))) {
            try {
                otherAuth = Firebase.Auth.FirebaseAuth.GetAuth(Firebase.FirebaseApp.Create(
                    otherAuthOptions, "Secondary"));
                otherAuth.StateChanged += AuthStateChanged;
                otherAuth.IdTokenChanged += IdTokenChanged;
            } catch (Exception) {
                
            }
        }
        AuthStateChanged(this, null);

        FirebaseApp app = FirebaseApp.DefaultInstance;
        // NOTE: You'll need to replace this url with your Firebase App's database
        // path in order for the database connection to work correctly in editor.
        app.SetEditorDatabaseUrl("https://fir-demo-7d7a0.firebaseio.com/");
        if (app.Options.DatabaseUrl != null) app.SetEditorDatabaseUrl(app.Options.DatabaseUrl);
        StartListener();
    }

    protected void StartListener() {
        FirebaseDatabase.DefaultInstance
            .GetReference("Leaders").OrderByChild("score")
            .ValueChanged += (object sender2, ValueChangedEventArgs e2) => {
            if (e2.DatabaseError != null) {
                Debug.LogError(e2.DatabaseError.Message);
                return;
            }
            Debug.Log("Received values for Leaders.");
            string title = leaderBoard[0].ToString();
            leaderBoard.Clear();
            leaderBoard.Add(title);
            if (e2.Snapshot != null && e2.Snapshot.ChildrenCount > 0) {
                foreach (var childSnapshot in e2.Snapshot.Children) {
                    if (childSnapshot.Child("score") == null
                        || childSnapshot.Child("score").Value == null) {
                        Debug.LogError("Bad data in sample.  Did you forget to call SetEditorDatabaseUrl with your project id?");
                        break;
                    } else {
                        Debug.Log("Leaders entry : " +
                            childSnapshot.Child("email").Value.ToString() + " - " +
                            childSnapshot.Child("score").Value.ToString());
                        leaderBoard.Insert(1, childSnapshot.Child("score").Value.ToString()
                            + "  " + childSnapshot.Child("email").Value.ToString());
                    }
                }
            }
        };
    }

    void AuthStateChanged(object sender, System.EventArgs eventArgs) {
        Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
        Firebase.Auth.FirebaseUser user = null;
        if (senderAuth != null) userByAuth.TryGetValue(senderAuth.App.Name, out user);
        if (senderAuth == auth && senderAuth.CurrentUser != user) {
            bool signedIn = user != senderAuth.CurrentUser && senderAuth.CurrentUser != null;
            if (!signedIn && user != null) {
                DebugLog("Signed out " + user.UserId);
            }
            user = senderAuth.CurrentUser;
            userByAuth[senderAuth.App.Name] = user;
            if (signedIn) {
                DebugLog("Signed in " + user.UserId);
                displayName = user.DisplayName ?? "";
                //DisplayDetailedUserInfo(user, 1);
            }
        }
    }

    void IdTokenChanged(object sender, System.EventArgs eventArgs) {
        Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
        if (senderAuth == auth && senderAuth.CurrentUser != null && !fetchingToken) {
            senderAuth.CurrentUser.TokenAsync(false).ContinueWith(
                task => DebugLog(String.Format("Token[0:8] = {0}", task.Result.Substring(0, 8))));
        }
    }

    protected bool LogTaskCompletion(Task task, string operation) {
        bool complete = false;
        if (task.IsCanceled) {
            DebugLog(operation + " canceled.");
        } else if (task.IsFaulted) {
            DebugLog(operation + " encounted an error.");
            foreach (Exception exception in task.Exception.Flatten().InnerExceptions) {
                string authErrorCode = "";
                Firebase.FirebaseException firebaseEx = exception as Firebase.FirebaseException;
                if (firebaseEx != null) {
                    authErrorCode = String.Format("AuthError.{0}: ",
                        ((Firebase.Auth.AuthError)firebaseEx.ErrorCode).ToString());
                }
                DebugLog(authErrorCode + exception.ToString());
            }
        } else if (task.IsCompleted) {
            DebugLog(operation + " completed");
            complete = true;
        }
        return complete;
    }

    public Task UpdateUserProfileAsync(string newDisplayName = null) {
        if (auth.CurrentUser == null) {
            DebugLog("Not signed in, unable to update user profile");
            return Task.FromResult(0);
        }
        displayName = newDisplayName ?? displayName;
        DebugLog("Updating user profile");
        return auth.CurrentUser.UpdateUserProfileAsync(new Firebase.Auth.UserProfile {
            DisplayName = displayName,
            PhotoUrl = auth.CurrentUser.PhotoUrl,
        }).ContinueWith(task => {
            if (LogTaskCompletion(task, "User profile")) {
                //DisplayDetailedUserInfo(auth.CurrentUser, 1);
            }
        });
    }

    void HandleSignInWithSignInResult(Task<Firebase.Auth.SignInResult> task) {
        if (LogTaskCompletion(task, "Sign-in")) {
            //DisplaySignInResult(task.Result, 1);
        }
    }

    void HandleSignInWithUser(Task<Firebase.Auth.FirebaseUser> task) {
        if (LogTaskCompletion(task, "Sign-in")) {
            //DebugLog(String.Format("{0} signed in", task.Result.DisplayName));
        }
    }














    public void _OnClick_SignIn()
    {
        var googleSignInScript = AndroidGoogleSignIn.Init(this.gameObject);
        googleSignInScript.SignIn(WEB_CLIENT_ID, GoogleSuccessCallback, GoogleErrorCallback);
    }

    void GoogleSuccessCallback(AndroidGoogleSignInAccount sucess_Args)
    {
        User_Email = sucess_Args.Email;
        Invoke("SignInWait",1);
    }

    void GoogleErrorCallback(string error_Args) { }


    void SignInWait()
    {
        _CreateUserWithEmailAsync();
    }

    void _CreateUserWithEmailAsync() {
        string newDisplayName = displayName;
        auth.CreateUserWithEmailAndPasswordAsync(User_Email, User_Password)
            .ContinueWith((task) => {
                if (LogTaskCompletion(task, "User Creation")) {
                    var user = task.Result;
                    //DisplayDetailedUserInfo(user, 1);
                    return UpdateUserProfileAsync(newDisplayName: newDisplayName);
                }
                return task;
            }).Unwrap();

        _SigninWithEmailAsync();
    }

    void _SigninWithEmailAsync() {

        if (signInAndFetchProfile) {
            auth.SignInAndRetrieveDataWithCredentialAsync(
                Firebase.Auth.EmailAuthProvider.GetCredential(User_Email, User_Password)).ContinueWith(
                    HandleSignInWithSignInResult);
        } else {
            auth.SignInWithEmailAndPasswordAsync(User_Email, User_Password)
                .ContinueWith(HandleSignInWithUser);
        }

        LoggerText.text = "Signing in as : " + auth.CurrentUser.Email;
        _CheckIfValid(auth.CurrentUser.UserId);
    }

    void _CheckIfValid(string uid)
    {
        DatabaseReference reference = FirebaseDatabase.DefaultInstance.GetReference("Users");//.Child(uid);

        reference.GetValueAsync().ContinueWith(task => {
            if (task.IsFaulted) {
                
            }
            else if (task.IsCompleted) {
                DataSnapshot snapshot = task.Result;
                if(snapshot.HasChild(uid))
                {
                    Access_Granted();
                }
                else
                {
                    Access_Denied();
                }
            }
        });
    }

    void Access_Granted()
    {
        LoggerText.text = "Access granted";
    }

    void Access_Denied()
    {
        LoggerText.text = "Access denied";
    }

}
