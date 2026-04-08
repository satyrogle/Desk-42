using UnityEngine;
using UnityEngine.UI;
using Desk42.Core;
using Desk42.Persistence;
using System.Linq;

namespace Desk42.UI
{
    /// <summary>
    /// Renders the Internal Audit Meta-Hub (Main Menu) where players spend
    /// Corporate Credits to buy handbook benefits or review Repeat Offenders.
    /// </summary>
    public class InternalAuditMetaHubUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMPro.TextMeshProUGUI _creditBalanceText;
        [SerializeField] private Button _buyHealthInsuranceBtn;
        [SerializeField] private Button _buySigningBonusBtn;
        [SerializeField] private Button _startNextShiftBtn;

        private MetaProgressData _meta;

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.Meta != null)
                Initialize(GameManager.Instance.Meta);

            if (_buyHealthInsuranceBtn != null)
                _buyHealthInsuranceBtn.onClick.AddListener(() => TryBuyBenefit("health_insurance", 150));
            if (_buySigningBonusBtn != null)
                _buySigningBonusBtn.onClick.AddListener(() => TryBuyBenefit("signing_bonus", 50));
            
            if (_startNextShiftBtn != null)
                _startNextShiftBtn.onClick.AddListener(() => GameManager.Instance.StartNewRun("auditor")); // Replace with correct archetype picker
        }

        private void OnDestroy()
        {
            // Removed OnMetaProgressLoaded unsubscription
        }

        private void Initialize(MetaProgressData meta)
        {
            _meta = meta;
            RefreshUI();
        }

        private void TryBuyBenefit(string benefitId, int cost)
        {
            if (_meta == null) return;

            if (_meta.EmployeeHandbook.HasBenefit(benefitId))
            {
                Debug.Log($"[MetaHub] Already own benefit {benefitId}.");
                return; // already unlocked
            }

            if (_meta.BankBalance < cost)
            {
                Debug.Log($"[MetaHub] Not enough credits for {benefitId}. Costs {cost}, have {_meta.BankBalance}.");
                return;
            }

            _meta.BankBalance -= cost;
            _meta.EmployeeHandbook.UnlockBenefit(benefitId);
            GameManager.Instance.SaveSystem.SaveMeta(GameManager.Instance.Meta);
            RefreshUI();
            
            Debug.Log($"[MetaHub] Purchased benefit {benefitId} for {cost} credits. Remaining: {_meta.BankBalance}.");
        }

        private void RefreshUI()
        {
            if (_meta == null) return;
            
            // Check button states based on unlocked benefits
            if (_buyHealthInsuranceBtn != null)
                _buyHealthInsuranceBtn.interactable = !_meta.EmployeeHandbook.HasBenefit("health_insurance");
            if (_buySigningBonusBtn != null)
                _buySigningBonusBtn.interactable = !_meta.EmployeeHandbook.HasBenefit("signing_bonus");

            if (_creditBalanceText != null)
                _creditBalanceText.text = $"Banked Credits: ¢{_meta.BankBalance}"; 
        }
    }
}
