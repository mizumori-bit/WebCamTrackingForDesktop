import cv2
import mediapipe as mp
import numpy as np
from pythonosc import udp_client
import math
import traceback
import sys
import time

# MediaPipe初期化
mp_drawing = mp.solutions.drawing_utils
mp_pose = mp.solutions.pose
mp_hands = mp.solutions.hands

# OSC設定
try:
    osc_client = udp_client.SimpleUDPClient("127.0.0.1", 9000)  # VRChatの受信ポートに変更
    print("OSC client initialized successfully on port 9000")
except Exception as e:
    print(f"Failed to initialize OSC client: {e}")
    traceback.print_exc()
    sys.exit(1)

# パフォーマンス設定
TARGET_FPS = 30
FRAME_INTERVAL = 1.0 / TARGET_FPS
LOG_INTERVAL = 0.5  # ログを0.5秒に1回に制限
MOVEMENT_SCALE = 2.0  # 動作範囲のスケール係数
VALUE_CHANGE_THRESHOLD = 0.01  # 値の変化閾値

# 検出信頼度しきい値
DETECTION_THRESHOLD = 0.0  # トラッキングテスト用に閾値を0に設定
SMOOTH_FACTOR = 0.5  # 値の平滑化係数

class OscLogger:
    def __init__(self):
        self.last_log_time = {}
        self.last_values = {}

    def should_log(self, address, value, current_time):
        # 前回のログ時刻と値を取得
        last_time = self.last_log_time.get(address, 0)
        last_value = self.last_values.get(address, None)

        # 値の変化量をチェック
        value_changed = (last_value is None or 
                        abs(value - last_value) > VALUE_CHANGE_THRESHOLD)

        # ログ間隔をチェック
        time_elapsed = current_time - last_time

        if time_elapsed >= LOG_INTERVAL and value_changed:
            self.last_log_time[address] = current_time
            self.last_values[address] = value
            return True
        return False

    def log_message(self, address, value):
        current_time = time.time()
        if self.should_log(address, value, current_time):
            print(f"Sending OSC: {address} = {value}")

def send_osc_message(address, value, logger):
    """OSCメッセージを送信"""
    try:
        logger.log_message(address, value)
        osc_client.send_message(address, value)
    except Exception as e:
        print(f"Failed to send OSC message to {address}: {e}")

class BodyTracker:
    def __init__(self):
        # 前フレームの値を保持
        self.prev_body_rotation = 0
        self.prev_left_arm_x = 0
        self.prev_left_arm_height = 0
        self.prev_right_arm_x = 0
        self.prev_right_arm_height = 0
        self.prev_left_leg_lift = 0
        self.prev_right_leg_lift = 0
        self.osc_logger = OscLogger()

    def smooth_value(self, current, previous):
        """値を平滑化する"""
        return current * (1 - SMOOTH_FACTOR) + previous * SMOOTH_FACTOR

    def scale_position(self, value):
        """位置の値をスケーリング"""
        return value * MOVEMENT_SCALE

    def detect_body_rotation(self, pose_landmarks):
        """体の回転を検出"""
        left_shoulder = pose_landmarks.landmark[mp_pose.PoseLandmark.LEFT_SHOULDER]
        right_shoulder = pose_landmarks.landmark[mp_pose.PoseLandmark.RIGHT_SHOULDER]
        
        # 両肩の検出信頼度をチェック
        if (left_shoulder.visibility > DETECTION_THRESHOLD and 
            right_shoulder.visibility > DETECTION_THRESHOLD):
            # 肩の位置から体の回転を計算
            shoulder_diff = right_shoulder.x - left_shoulder.x
            rotation = shoulder_diff / 0.5  # -1から1の範囲に正規化
            rotation = self.scale_position(rotation)  # スケーリング
            rotation = max(-1, min(1, rotation))  # クランプ
            rotation = self.smooth_value(rotation, self.prev_body_rotation)
            self.prev_body_rotation = rotation
            
            send_osc_message("/avatar/parameters/BodyRotation", rotation, self.osc_logger)
            send_osc_message("/avatar/parameters/BodyDetected", 1.0, self.osc_logger)
        else:
            send_osc_message("/avatar/parameters/BodyDetected", 0.0, self.osc_logger)

    def detect_arm_position(self, pose_landmarks, side):
        """腕の位置を検出"""
        if side == "Left":
            shoulder = pose_landmarks.landmark[mp_pose.PoseLandmark.LEFT_SHOULDER]
            elbow = pose_landmarks.landmark[mp_pose.PoseLandmark.LEFT_ELBOW]
            wrist = pose_landmarks.landmark[mp_pose.PoseLandmark.LEFT_WRIST]
            prev_x = self.prev_left_arm_x
            prev_height = self.prev_left_arm_height
        else:
            shoulder = pose_landmarks.landmark[mp_pose.PoseLandmark.RIGHT_SHOULDER]
            elbow = pose_landmarks.landmark[mp_pose.PoseLandmark.RIGHT_ELBOW]
            wrist = pose_landmarks.landmark[mp_pose.PoseLandmark.RIGHT_WRIST]
            prev_x = self.prev_right_arm_x
            prev_height = self.prev_right_arm_height

        if (shoulder.visibility > DETECTION_THRESHOLD and 
            elbow.visibility > DETECTION_THRESHOLD and 
            wrist.visibility > DETECTION_THRESHOLD):
            # 前後の位置（X軸）
            x_pos = (wrist.z - shoulder.z)
            x_pos = self.scale_position(x_pos)  # スケーリング
            x_pos = max(-1, min(1, x_pos))  # クランプ
            x_pos = self.smooth_value(x_pos, prev_x)

            # 高さ（Y軸）
            height = (shoulder.y - wrist.y)
            height = self.scale_position(height)  # スケーリング
            height = max(-1, min(1, height))  # クランプ
            height = self.smooth_value(height, prev_height)

            if side == "Left":
                self.prev_left_arm_x = x_pos
                self.prev_left_arm_height = height
            else:
                self.prev_right_arm_x = x_pos
                self.prev_right_arm_height = height

            send_osc_message(f"/avatar/parameters/{side}ArmX", x_pos, self.osc_logger)
            send_osc_message(f"/avatar/parameters/{side}ArmHeight", height, self.osc_logger)
            send_osc_message(f"/avatar/parameters/{side}ArmDetected", 1.0, self.osc_logger)
        else:
            send_osc_message(f"/avatar/parameters/{side}ArmDetected", 0.0, self.osc_logger)

    def detect_leg_position(self, pose_landmarks, side):
        """脚の位置を検出"""
        if side == "Left":
            hip = pose_landmarks.landmark[mp_pose.PoseLandmark.LEFT_HIP]
            knee = pose_landmarks.landmark[mp_pose.PoseLandmark.LEFT_KNEE]
            ankle = pose_landmarks.landmark[mp_pose.PoseLandmark.LEFT_ANKLE]
            prev_lift = self.prev_left_leg_lift
        else:
            hip = pose_landmarks.landmark[mp_pose.PoseLandmark.RIGHT_HIP]
            knee = pose_landmarks.landmark[mp_pose.PoseLandmark.RIGHT_KNEE]
            ankle = pose_landmarks.landmark[mp_pose.PoseLandmark.RIGHT_ANKLE]
            prev_lift = self.prev_right_leg_lift

        if (hip.visibility > DETECTION_THRESHOLD and 
            knee.visibility > DETECTION_THRESHOLD and 
            ankle.visibility > DETECTION_THRESHOLD):
            # 脚の持ち上げ具合を計算
            lift = (hip.y - knee.y) * 2
            lift = self.scale_position(lift)  # スケーリング
            lift = max(0, min(1, lift))  # クランプ
            lift = self.smooth_value(lift, prev_lift)

            if side == "Left":
                self.prev_left_leg_lift = lift
            else:
                self.prev_right_leg_lift = lift

            send_osc_message(f"/avatar/parameters/{side}LegLift", lift, self.osc_logger)
            send_osc_message(f"/avatar/parameters/{side}LegDetected", 1.0, self.osc_logger)
        else:
            send_osc_message(f"/avatar/parameters/{side}LegDetected", 0.0, self.osc_logger)

    def detect_hand_gesture(self, hand_landmarks, side):
        """手のジェスチャーを検出"""
        if hand_landmarks:
            # 指先の検出状態をチェック
            index_tip = hand_landmarks.landmark[mp_hands.HandLandmark.INDEX_FINGER_TIP]
            middle_tip = hand_landmarks.landmark[mp_hands.HandLandmark.MIDDLE_FINGER_TIP]
            ring_tip = hand_landmarks.landmark[mp_hands.HandLandmark.RING_FINGER_TIP]
            pinky_tip = hand_landmarks.landmark[mp_hands.HandLandmark.PINKY_TIP]
            
            if all(l.visibility > DETECTION_THRESHOLD for l in [index_tip, middle_tip, ring_tip, pinky_tip]):
                # ジェスチャー判定ロジックを実装
                # 例：指の曲げ具合でジェスチャーを判定
                gesture = 0  # デフォルト
                # ここにジェスチャー判定ロジックを追加
                
                send_osc_message(f"/avatar/parameters/{side}HandGesture", gesture, self.osc_logger)
                send_osc_message(f"/avatar/parameters/{side}HandDetected", 1.0, self.osc_logger)
                return
        
        send_osc_message(f"/avatar/parameters/{side}HandDetected", 0.0, self.osc_logger)

def optimize_camera(cap):
    """カメラの設定を最適化"""
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)  # 解像度を設定
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    cap.set(cv2.CAP_PROP_FPS, TARGET_FPS)  # FPSを設定
    return cap

def main():
    print("Starting VRChat Full Body Tracking...")
    print(f"Target FPS: {TARGET_FPS}")
    print("OSC messages will be sent to VRChat on port 9000")
    print("Press ESC to exit")

    try:
        cap = cv2.VideoCapture(0)
        if not cap.isOpened():
            raise Exception("Failed to open camera")
        
        cap = optimize_camera(cap)
        tracker = BodyTracker()
        last_frame_time = time.time()

        with mp_pose.Pose(
            min_detection_confidence=0.1,  # 検出信頼度閾値を下げる
            min_tracking_confidence=0.1) as pose, \
            mp_hands.Hands(
            min_detection_confidence=0.1,  # 検出信頼度閾値を下げる
            min_tracking_confidence=0.1) as hands:
            
            while cap.isOpened():
                # フレームレート制御
                current_time = time.time()
                elapsed = current_time - last_frame_time
                if elapsed < FRAME_INTERVAL:
                    # 次のフレームまで待機
                    continue

                last_frame_time = current_time

                success, image = cap.read()
                if not success:
                    print("Failed to read camera frame")
                    continue

                image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
                image.flags.writeable = False
                
                # ポーズ検出
                pose_results = pose.process(image)
                if pose_results.pose_landmarks:
                    tracker.detect_body_rotation(pose_results.pose_landmarks)
                    tracker.detect_arm_position(pose_results.pose_landmarks, "Left")
                    tracker.detect_arm_position(pose_results.pose_landmarks, "Right")
                    tracker.detect_leg_position(pose_results.pose_landmarks, "Left")
                    tracker.detect_leg_position(pose_results.pose_landmarks, "Right")
                
                # 手の検出
                image.flags.writeable = True
                hands_results = hands.process(image)
                
                # 左右の手を識別して処理
                if hands_results.multi_hand_landmarks:
                    for hand_landmarks in hands_results.multi_hand_landmarks:
                        # 手の左右を判定
                        side = "Left" if hand_landmarks.landmark[mp_hands.HandLandmark.WRIST].x < 0.5 else "Right"
                        tracker.detect_hand_gesture(hand_landmarks, side)

                # デバッグ表示
                image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)
                if pose_results.pose_landmarks:
                    mp_drawing.draw_landmarks(
                        image, pose_results.pose_landmarks, mp_pose.POSE_CONNECTIONS)
                if hands_results.multi_hand_landmarks:
                    for hand_landmarks in hands_results.multi_hand_landmarks:
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS)
                
                # FPS表示
                cv2.putText(image, f"FPS: {int(1/elapsed)}", (10, 30), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                
                cv2.imshow('MediaPipe Pose', image)
                if cv2.waitKey(5) & 0xFF == 27:
                    print("ESC pressed, exiting...")
                    break

    except Exception as e:
        print(f"Error in main loop: {e}")
        traceback.print_exc()
    finally:
        if 'cap' in locals():
            cap.release()
        cv2.destroyAllWindows()
        print("Application terminated")

if __name__ == "__main__":
    main()
