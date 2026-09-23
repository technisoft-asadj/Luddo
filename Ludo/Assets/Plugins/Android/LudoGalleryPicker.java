package com.devorbit.ludofight;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.net.Uri;
import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

/**
 * Lets the player pick one picture from the phone's gallery. It uses Android's own picker (ACTION_GET_CONTENT), so the
 * game needs NO storage or photo permission and never sees any other picture. The chosen picture is turned upright,
 * cut to a centred square and shrunk to size x size, saved as a small JPEG in the app's private cache, and its path is
 * sent back to Unity ("cancel" or "error:..." when nothing was chosen).
 */
public class LudoGalleryPicker {
    static final int REQUEST = 47110;

    /** Called from Unity. */
    public static void pick(final Activity activity, final String receiver, final String method, final int size) {
        activity.runOnUiThread(new Runnable() {
            public void run() {
                try {
                    PickerFragment fragment = new PickerFragment();
                    fragment.receiver = receiver;
                    fragment.method = method;
                    fragment.size = size;
                    activity.getFragmentManager().beginTransaction().add(fragment, "ludo_gallery_picker").commit();
                } catch (Exception e) {
                    send(receiver, method, "error:" + e.getMessage());
                }
            }
        });
    }

    static void send(String receiver, String method, String message) {
        UnityPlayer.UnitySendMessage(receiver, method, message);
    }

    /** An invisible screen-less fragment: it owns the "pick a picture" request so the result comes back to it. */
    public static class PickerFragment extends Fragment {
        String receiver = "";
        String method = "";
        int size = 256;
        boolean started;

        @Override
        public void onStart() {
            super.onStart();
            if (started) return;
            started = true;
            try {
                Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
                intent.setType("image/*");
                intent.addCategory(Intent.CATEGORY_OPENABLE);
                startActivityForResult(intent, REQUEST);
            } catch (Exception e) {
                finish("error:" + e.getMessage());
            }
        }

        @Override
        public void onActivityResult(int requestCode, final int resultCode, final Intent data) {
            super.onActivityResult(requestCode, resultCode, data);
            if (requestCode != REQUEST) return;
            if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
                finish("cancel");
                return;
            }
            final Uri uri = data.getData();
            final Activity activity = getActivity();
            new Thread(new Runnable() {
                public void run() {
                    String result;
                    try {
                        result = prepare(activity, uri, size);
                    } catch (Throwable t) {
                        result = "error:" + t.getMessage();
                    }
                    finish(result);
                }
            }).start();
        }

        void finish(String message) {
            send(receiver, method, message);
            try {
                Activity activity = getActivity();
                if (activity != null) activity.getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
            } catch (Exception ignored) {
            }
        }
    }

    static String prepare(Activity activity, Uri uri, int size) throws Exception {
        // 1. how big is it? (decode nothing yet)
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        InputStream in = activity.getContentResolver().openInputStream(uri);
        BitmapFactory.decodeStream(in, null, bounds);
        if (in != null) in.close();
        if (bounds.outWidth <= 0 || bounds.outHeight <= 0) throw new Exception("not a picture");

        // 2. decode a reduced copy (never the full 12-megapixel photo)
        int sample = 1;
        while (bounds.outWidth / (sample * 2) >= size * 2 && bounds.outHeight / (sample * 2) >= size * 2) sample *= 2;
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = sample;
        in = activity.getContentResolver().openInputStream(uri);
        Bitmap bitmap = BitmapFactory.decodeStream(in, null, options);
        if (in != null) in.close();
        if (bitmap == null) throw new Exception("could not read the picture");

        // 3. turn it upright (phones store photos sideways and add an "orientation" tag)
        int degrees = 0;
        try {
            InputStream exifStream = activity.getContentResolver().openInputStream(uri);
            if (exifStream != null) {
                int o = new ExifInterface(exifStream).getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL);
                exifStream.close();
                if (o == ExifInterface.ORIENTATION_ROTATE_90) degrees = 90;
                else if (o == ExifInterface.ORIENTATION_ROTATE_180) degrees = 180;
                else if (o == ExifInterface.ORIENTATION_ROTATE_270) degrees = 270;
            }
        } catch (Exception ignored) {
        }

        // 4. centred square, scaled to size x size
        int side = Math.min(bitmap.getWidth(), bitmap.getHeight());
        int x = (bitmap.getWidth() - side) / 2;
        int y = (bitmap.getHeight() - side) / 2;
        Matrix matrix = new Matrix();
        matrix.postScale((float) size / side, (float) size / side);
        if (degrees != 0) matrix.postRotate(degrees);
        Bitmap square = Bitmap.createBitmap(bitmap, x, y, side, side, matrix, true);

        File file = new File(activity.getCacheDir(), "ludo_picked_photo.jpg");
        FileOutputStream out = new FileOutputStream(file);
        square.compress(Bitmap.CompressFormat.JPEG, 90, out);
        out.close();
        return file.getAbsolutePath();
    }
}
