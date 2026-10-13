import cv2
import mediapipe as mp
import json
import sys
import os

def extract_pose(video_path, output_json_path):
    if not os.path.exists(video_path):
        print(f"Error: Video file not found: {video_path}")
        sys.exit(1)

    mp_pose = mp.solutions.pose

    # static_image_mode=False video için, model_complexity=2 max doğruluk
    pose = mp_pose.Pose(
        static_image_mode=False,
        model_complexity=2,
        enable_segmentation=False,
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5
    )

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"Error: Could not open video file: {video_path}")
        sys.exit(1)

    # Bilgi amaçlı nominal FPS (PTS için kullanılmıyor)
    nominal_fps = cap.get(cv2.CAP_PROP_FPS)
    if nominal_fps <= 0:
        nominal_fps = 30.0

    frames_data = []
    frame_index = 0

    while True:
        ret, frame = cap.read()
        if not ret:
            break

        # PTS, kare okunduktan SONRA alınır: CAP_PROP_POS_MSEC son okunan karenin zaman damgasını verir.
        # Okumadan önce alınırsa her kare bir önceki karenin zamanını taşır (ilk iki kare 0.0 olur).
        pts_ms = cap.get(cv2.CAP_PROP_POS_MSEC)
        timestamp_sec = pts_ms / 1000.0

        # MediaPipe RGB ister
        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = pose.process(image_rgb)

        keypoints = {}
        if results.pose_landmarks:
            height, width, _ = frame.shape

            for idx, lm in enumerate(results.pose_landmarks.landmark):
                # Normalize koordinatları piksel koordinatlarına çevir
                x_px = lm.x * width
                y_px = lm.y * height

                keypoints[str(idx)] = {
                    "x": x_px,
                    "y": y_px,
                    "confidence": float(lm.visibility)
                }

        frames_data.append({
            "timestampSec": timestamp_sec,
            "landmarks": keypoints
        })

        frame_index += 1

    cap.release()
    pose.close()

    output_data = {
        "fps": nominal_fps,
        "frameCount": frame_index,
        "frames": frames_data
    }

    with open(output_json_path, 'w', encoding='utf-8') as f:
        json.dump(output_data, f)

    print(f"Successfully processed {frame_index} frames. Output saved to {output_json_path}")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python extract_pose.py <video_path> <output_json_path>")
        sys.exit(1)

    video_path_arg = sys.argv[1]
    output_json_path_arg = sys.argv[2]

    extract_pose(video_path_arg, output_json_path_arg)

