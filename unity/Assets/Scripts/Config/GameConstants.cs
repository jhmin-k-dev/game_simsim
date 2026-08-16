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
        public const float CameraPitchDeg = 15f;     // ★ 부감 각도, 배경 프롬프트와 동일
        public const float CameraDistance = 12f;     // 초안값
        public const float CameraHeight = 3.2f;      // 초안값 (지면 기준)
        public const float HorizonViewportY = 0.62f; // ★ 배경 지평선 위치 (02 §2-2)

        // ---- SafeBox (02 §2-3) ----
        public const float SafeBoxHalfX = 0.18f;     // 가로 ±18% (뷰포트)
        public const float SafeBoxHalfY = 0.10f;     // 세로 ±10%
        public const float SafeBoxCenterX = 0.5f;
        public const float SafeBoxCenterY = 0.55f;   // 지평선 62% 정합용 (03 §1-1 Screen Y)
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
    }
}
