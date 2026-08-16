namespace Nurungi.Config
{
    /// <summary>
    /// 프로젝트 불변 상수 (CLAUDE.md, 02_기술사양 §8-2: 매직 넘버는 여기에만).
    /// ★ 표시 값은 문서 확정값 — 변경은 디렉터 승인 필요.
    /// </summary>
    public static class GameConstants
    {
        // ---- 카메라 (02 §2-1) ----
        public const float CameraFov = 28f;          // ★ 바꾸면 배경 625장이 어긋남
        // ⚠ 부감 15°(★)는 FOV 28과 조합 시 하늘이 화면에 안 들어옴(시야 상단 -1°).
        //    참조 영상의 하늘 여백 재현을 위해 8°로 조정 — 02 §2-2 "지평선 62% 정합 튜닝" 근거.
        //    디렉터 확정 필요 (확정 시 배경 생성 프롬프트도 "8° 부감"으로 통일할 것).
        public const float CameraPitchDeg = 3f;
        public const float CameraDistance = 11f;     // 누룽이(0.9m)가 화면 높이의 ~16% (참조 영상 비율)
        public const float CameraHeight = 2.4f;      // 지면 기준 — 하늘 여백이 화면 절반이 되는 높이
        public const float HorizonViewportY = 0.62f; // ★ 배경 지평선 위치 (02 §2-2)

        // ---- SafeBox (02 §2-3) ----
        public const float SafeBoxHalfX = 0.18f;     // 가로 ±18% (뷰포트)
        public const float SafeBoxHalfY = 0.10f;     // 세로 ±10%
        public const float SafeBoxCenterX = 0.5f;
        public const float SafeBoxCenterY = 0.25f;   // 캐릭터를 화면 하단에 두어 하늘 여백 확보 (참조 영상 구도)
        public const float CameraDampX = 0.25f;      // 가로 SmoothDamp
        public const float CameraDampY = 0.45f;      // 세로 (느리게)
        public const float CameraMaxLead = 1.5f;     // 속도 리드 최대
        public const float CameraLeadBlend = 0.4f;   // 방향 전환 반영 시간
        public const float JumpVerticalDampMul = 0.6f; // 점프 중 세로 추적 억제

        // ---- 세로 모드 (02 §2-4) ----
        public const float PortraitDistanceMul = 1.35f;
        public const float PortraitSafeBoxHalfX = 0.12f;
        public const float PortraitSafeBoxHalfY = 0.14f;

        // ---- 이동 (01 §3-3, 초안 — M1에서 손으로 조율) ----
        public const float WalkSpeed = 2.2f;
        public const float RunSpeed = 4.5f;
        public const float MoveAccel = 12f;
        public const float MoveDecel = 16f;
        public const float TurnSpeedWalkDeg = 540f;
        public const float TurnSpeedRunDeg = 720f;
        public const float InputDeadZone = 0.15f;    // 01 §3-2

        // ---- 하이브리드 보행 (01 §3-3, §4-4) ----
        public const float StanceTransitionSeconds = 0.3f;  // 2족↔4족 전환. 속도는 유지
        public const float SprintStickThreshold = 0.8f;     // 스틱 깊이 이상이면 질주

        // ---- 점프 (01 §4-4) ----
        public const float JumpHeight = 0.9f;        // 누룽이 키만큼
        public const float Gravity = -16f;           // 만화적 무게감: 실제 중력보다 무겁게
        public const float JumpBufferSeconds = 0.12f;// 착지 직전 입력을 받아둠
        public const float CoyoteSeconds = 0.10f;    // 발판을 막 벗어난 직후에도 점프 허용
        public const float AirControlMul = 0.55f;    // 공중 방향 전환 제한
    }
}
