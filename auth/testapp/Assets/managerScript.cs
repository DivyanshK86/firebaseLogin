using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class managerScript : MonoBehaviour {

    public Text errorTxt;

    string WEB_CLIENT_ID = "129044841185-5s9p870updhpblbn4331e9g0reubdp1c.apps.googleusercontent.com";
    public UIHandler uiHandler;

    public void _SignIn()
    {
        var googleSignInScript = AndroidGoogleSignIn.Init(this.gameObject);
        googleSignInScript.SignIn(WEB_CLIENT_ID, GoogleSuccessCallback, GoogleErrorCallback);
    }

    void GoogleSuccessCallback(AndroidGoogleSignInAccount s)
    {
        //errorTxt.text = s.Token;
        uiHandler.email = s.Email;
        uiHandler.password = "abcd@12345";
        uiHandler._CreateUserWithEmailAsync();
    }

    void GoogleErrorCallback(string e)
    {
        errorTxt.text = e;
    }
}
