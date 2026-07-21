using UnityEngine;

namespace MiniGame1
{
    // 시즌 브랜드 데이터 — 씬 수정 없이 에셋 교체만으로 브랜드를 갈아끼운다 (mini-game-1-prd.md §3.1).
    [CreateAssetMenu(fileName = "BrandConfig", menuName = "MiniGame1/Brand Config")]
    public class BrandConfig : ScriptableObject
    {
        public string brandName = "바잇미";
        public string productName = "강아지 장난감";
        // 협업 계약 전에는 반드시 노출한다 (§3.4 정직성 규칙)
        public string partnershipLabel = "모의 협업";
        public Sprite logo;
        // 자사몰(어필리에이트) 링크 — 결과 화면 배너에서 새 탭으로 연다 (상위 PRD §7.6)
        public string storeUrl = "";
        public int bonusScore = 300;
        public float dropIntervalSec = 15f;
        public float demoDropIntervalSec = 8f;
    }
}
