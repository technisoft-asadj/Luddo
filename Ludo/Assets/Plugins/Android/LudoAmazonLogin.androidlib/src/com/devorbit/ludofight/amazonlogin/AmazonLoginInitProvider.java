package com.devorbit.ludofight.amazonlogin;

import android.app.Activity;
import android.app.Application;
import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;

/**
 * Runs automatically at app start (declared in AndroidManifest.xml, same pattern as Google Play Games' own
 * PlayGamesInitProvider and AdMob's MobileAdsInitProvider in this project) so Login with Amazon can be told
 * about every Activity resume without Ludo Fight's launcher activity having to change or subclass anything.
 */
public class AmazonLoginInitProvider extends ContentProvider {
    @Override
    public boolean onCreate() {
        Context context = getContext();
        if (context != null) {
            Application app = (Application) context.getApplicationContext();
            app.registerActivityLifecycleCallbacks(new Application.ActivityLifecycleCallbacks() {
                @Override public void onActivityResumed(Activity activity) { AmazonLoginBridge.onActivityResumed(); }
                @Override public void onActivityCreated(Activity activity, Bundle savedInstanceState) { }
                @Override public void onActivityStarted(Activity activity) { }
                @Override public void onActivityPaused(Activity activity) { }
                @Override public void onActivityStopped(Activity activity) { }
                @Override public void onActivitySaveInstanceState(Activity activity, Bundle outState) { }
                @Override public void onActivityDestroyed(Activity activity) { }
            });
        }
        return true;
    }

    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) { return null; }
    @Override public String getType(Uri uri) { return null; }
    @Override public Uri insert(Uri uri, ContentValues values) { return null; }
    @Override public int delete(Uri uri, String selection, String[] selectionArgs) { return 0; }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) { return 0; }
}
