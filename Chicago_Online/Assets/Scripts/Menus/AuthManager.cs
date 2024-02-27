using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using TMPro;
using System.Threading.Tasks;
using Firebase.Database;

public class AuthManager : MonoBehaviour
{
    #region Singleton
    public static AuthManager instance;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(this);
    }
    #endregion

    [Header("Firebase")]
    public DependencyStatus dependencyStatus;
    public FirebaseAuth auth;
    public FirebaseUser user;

    [Header("Login")]
    public TMP_InputField emailLoginField;
    public TMP_InputField passwordLoginField;
    public TMP_Text warningLoginText;
    public TMP_Text confirmLoginText;

    [Header("Register")]
    public TMP_InputField usernameRegisterField;
    public TMP_InputField emailRegisterField;
    public TMP_InputField passwordRegisterField;
    public TMP_InputField passwordRegisterVerifyField;
    public TMP_Text warningRegisterText;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                InitializeFirebase();
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    private void InitializeFirebase()
    {
        auth = FirebaseAuth.DefaultInstance;
    }

    #region Login
    public void LoginButton()
    {
        StartCoroutine(Login(emailLoginField.text, passwordLoginField.text));
    }

    private IEnumerator Login(string _email, string _password)
    {
        Task<AuthResult> LoginTask = auth.SignInWithEmailAndPasswordAsync(_email, _password);
        yield return new WaitUntil(predicate: () => LoginTask.IsCompleted);

        if (LoginTask.Exception != null)
        {
            Debug.LogWarning(message: $"Failed to register task with {LoginTask.Exception}");
            FirebaseException firebaseEx = LoginTask.Exception.GetBaseException() as FirebaseException;
            AuthError errorCode = (AuthError)firebaseEx.ErrorCode;

            string message = "Login Failed!";
            switch (errorCode)
            {
                case AuthError.MissingEmail:
                    message = "Missing Email";
                    break;
                case AuthError.MissingPassword:
                    message = "Missing Password";
                    break;
                case AuthError.WrongPassword:
                    message = "Wrong Password";
                    break;
                case AuthError.InvalidEmail:
                    message = "Invalid Email";
                    break;
                case AuthError.UserNotFound:
                    message = "Account does not exist";
                    break;
            }
            warningLoginText.text = message;
        }
        else
        {
            user = LoginTask.Result.User;
            Debug.LogFormat("User signed in successfully: {0} ({1})", user.DisplayName, user.Email);
            warningLoginText.text = "";
            confirmLoginText.text = "Logged In";

            DataSaver.instance.userId = user.UserId;
            DataSaver.instance.LoadData();

            yield return new WaitForSeconds(1);
            SceneHandler.instance.LoadServerScene();
        }
    }
    #endregion

    #region Register
    public void RegisterButton()
    {
        StartCoroutine(Register(emailRegisterField.text, passwordRegisterField.text, usernameRegisterField.text.ToLower()));
    }
    private IEnumerator Register(string _email, string _password, string _username)
    {
        bool isUsernameAvailable = false;
        Task<bool> checkUsernameTask = CheckUsernameAvailability(_username);
        yield return new WaitUntil(() => checkUsernameTask.IsCompleted);

        isUsernameAvailable = checkUsernameTask.Result;
        if (isUsernameAvailable)
        {
            if (_username == "")
            {
                warningRegisterText.text = "Missing Username";
            }
            else if (passwordRegisterField.text != passwordRegisterVerifyField.text)
            {
                warningRegisterText.text = "Password Does Not Match!";
            }
            else
            {
                Task<AuthResult> RegisterTask = auth.CreateUserWithEmailAndPasswordAsync(_email, _password);
                yield return new WaitUntil(predicate: () => RegisterTask.IsCompleted);

                if (RegisterTask.Exception != null)
                {
                    //If there are errors handle them
                    Debug.LogWarning(message: $"Failed to register task with {RegisterTask.Exception}");
                    FirebaseException firebaseEx = RegisterTask.Exception.GetBaseException() as FirebaseException;
                    AuthError errorCode = (AuthError)firebaseEx.ErrorCode;

                    string message = "Register Failed!";
                    switch (errorCode)
                    {
                        case AuthError.MissingEmail:
                            message = "Missing Email";
                            break;
                        case AuthError.MissingPassword:
                            message = "Missing Password";
                            break;
                        case AuthError.WeakPassword:
                            message = "Weak Password";
                            break;
                        case AuthError.EmailAlreadyInUse:
                            message = "Email Already In Use";
                            break;
                    }
                    warningRegisterText.text = message;
                }
                else
                {
                    user = RegisterTask.Result.User;

                    if (user != null)
                    {
                        UserProfile profile = new UserProfile { DisplayName = _username };
                        Task ProfileTask = user.UpdateUserProfileAsync(profile);
                        yield return new WaitUntil(predicate: () => ProfileTask.IsCompleted);

                        if (ProfileTask.Exception != null)
                        {
                            Debug.LogWarning(message: $"Failed to register task with {ProfileTask.Exception}");
                            FirebaseException firebaseEx = ProfileTask.Exception.GetBaseException() as FirebaseException;
                            AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
                            warningRegisterText.text = "Username Set Failed!";
                        }
                        else
                        {
                            DataSaver.instance.userId = user.UserId;
                            DataSaver.instance.dts.userName = user.DisplayName;
                            DataSaver.instance.dts.matchesWon = 0;
                            DataSaver.instance.SaveData();
                            SceneHandler.instance.LoginScreen();
                            warningRegisterText.text = "";
                        }
                    }
                }
            }
        }
    }
    private async Task<bool> CheckUsernameAvailability(string username)
    {
        DataSnapshot snapshot = await DataSaver.instance.dbRef.Child("users").GetValueAsync();

        if (snapshot.Exists)
        {
            foreach (var userSnapshot in snapshot.Children)
            {
                if (userSnapshot.Child("userName").Value.ToString() == username)
                {
                    warningRegisterText.text = "Username is taken";
                    return false;
                }
            }
        }
        return true;
    }
    #endregion
}
