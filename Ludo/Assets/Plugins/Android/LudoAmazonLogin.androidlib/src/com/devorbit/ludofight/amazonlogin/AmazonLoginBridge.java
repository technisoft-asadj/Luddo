package com.devorbit.ludofight.amazonlogin;

import android.app.Activity;

import com.amazon.identity.auth.device.AuthError;
import com.amazon.identity.auth.device.api.Listener;
import com.amazon.identity.auth.device.api.authorization.AuthCancellation;
import com.amazon.identity.auth.device.api.authorization.AuthorizationManager;
import com.amazon.identity.auth.device.api.authorization.AuthorizeListener;
import com.amazon.identity.auth.device.api.authorization.AuthorizeRequest;
import com.amazon.identity.auth.device.api.authorization.AuthorizeResult;
import com.amazon.identity.auth.device.api.authorization.ProfileScope;
import com.amazon.identity.auth.device.api.authorization.User;
import com.amazon.identity.auth.device.api.workflow.RequestContext;
import com.unity3d.player.UnityPlayer;

/**
 * "Continue with Amazon" for Unity (Online/AmazonLogin.cs calls these statics through AndroidJavaClass).
 * Unity Authentication has no built-in Amazon provider (unlike Google/Facebook), so this signs the player in
 * with Login with Amazon to read their name/e-mail only; the online profile itself stays on guest/account sign-in.
 * AmazonLoginInitProvider forwards every Activity's onResume() here, which the SDK needs to notice the browser
 * came back with a result - this avoids replacing Unity's own launcher activity.
 */
public class AmazonLoginBridge {
    static RequestContext requestContext;
    static String callbackGameObject = "";
    static boolean listenerReady;

    /** Called from Unity to start the sign-in. Results come back to the named GameObject: OnAmazonSuccess(name|||email|||userId),
     * OnAmazonError(message), OnAmazonCancel(). */
    public static void login(final Activity activity, String gameObject) {
        callbackGameObject = gameObject;
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    ensureListener(activity);
                    // profile() alone already returns the user's name, e-mail and user ID - Amazon rejects a
                    // separate ProfileScope.userId() scope with "400 Bad Request: An unknown scope was requested".
                    AuthorizationManager.authorize(
                            new AuthorizeRequest.Builder(requestContext)
                                    .addScopes(ProfileScope.profile())
                                    .build());
                } catch (Exception e) {
                    send("OnAmazonError", "Could not start Amazon sign-in: " + e.getMessage());
                }
            }
        });
    }

    /** Called from Unity on logout (Settings / Delete account): clears the Amazon-side sign-in state too. */
    public static void logout(final Activity activity) {
        AuthorizationManager.signOut(activity.getApplicationContext(), new Listener<Void, AuthError>() {
            @Override
            public void onSuccess(Void result) { }

            @Override
            public void onError(AuthError authError) { }
        });
    }

    static void ensureListener(Activity activity) {
        if (requestContext == null) requestContext = RequestContext.create(activity);
        if (listenerReady) return;
        listenerReady = true;
        requestContext.registerListener(new AuthorizeListener() {
            @Override
            public void onSuccess(AuthorizeResult result) {
                fetchProfile();
            }

            @Override
            public void onError(AuthError authError) {
                send("OnAmazonError", authError.getMessage() != null ? authError.getMessage() : "Amazon sign-in failed.");
            }

            @Override
            public void onCancel(AuthCancellation authCancellation) {
                send("OnAmazonCancel", "");
            }
        });
    }

    static void fetchProfile() {
        User.fetch(UnityPlayer.currentActivity, new Listener<User, AuthError>() {
            @Override
            public void onSuccess(User user) {
                String name = safe(user.getUserName());
                String email = safe(user.getUserEmail());
                String id = safe(user.getUserId());
                send("OnAmazonSuccess", name + "|||" + email + "|||" + id);
            }

            @Override
            public void onError(AuthError authError) {
                send("OnAmazonError", "Signed in, but could not read the Amazon profile. Try again.");
            }
        });
    }

    /** Called from AmazonLoginInitProvider's ActivityLifecycleCallbacks. */
    static void onActivityResumed() {
        if (requestContext != null) requestContext.onResume();
    }

    static String safe(String s) {
        return s == null ? "" : s.replace("|", "");
    }

    static void send(String method, String message) {
        if (callbackGameObject.length() == 0) return;
        UnityPlayer.UnitySendMessage(callbackGameObject, method, message);
    }
}
