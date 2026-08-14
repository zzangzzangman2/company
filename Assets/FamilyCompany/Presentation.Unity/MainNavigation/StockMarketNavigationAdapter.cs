using System;
using FamilyCompany.Simulation.Game;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    /// <summary>
    /// Navigation-only bridge. StockMarketFullscreenPanel continues to own the canonical
    /// market runtime, trading controls, GameState bridge, and save flush lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StockMarketNavigationAdapter : MonoBehaviour
    {
        private PrototypeBootstrap _bootstrap;
        private MainNavigationHudPresenter _presenter;
        private StockMarketFullscreenPanel _canonicalPanel;

        public StockMarketFullscreenPanel CanonicalPanel => _canonicalPanel;
        public GameState CanonicalGameState => _bootstrap != null ? _bootstrap.State : null;
        public bool IsStockMarketOpen => _canonicalPanel != null && _canonicalPanel.IsOpen;

        public void Configure(PrototypeBootstrap bootstrap, MainNavigationHudPresenter presenter)
        {
            _bootstrap = bootstrap != null ? bootstrap : throw new ArgumentNullException(nameof(bootstrap));
            _presenter = presenter != null ? presenter : throw new ArgumentNullException(nameof(presenter));
            var panels = bootstrap.GetComponents<StockMarketFullscreenPanel>();
            if (panels.Length > 1)
                throw new InvalidOperationException("Only one canonical StockMarketFullscreenPanel may exist.");
            _canonicalPanel = panels.Length == 1
                ? panels[0]
                : bootstrap.gameObject.AddComponent<StockMarketFullscreenPanel>();
        }

        public bool OpenFromInvestment()
        {
            if (_presenter == null || _presenter.ActiveTabId != "investment" || _canonicalPanel == null)
                return false;
            _canonicalPanel.OpenNow();
            return _canonicalPanel.IsOpen;
        }

        public bool TryHandleBackToInvestment()
        {
            if (_canonicalPanel == null || !_canonicalPanel.IsOpen) return false;
            _canonicalPanel.CloseNow();
            _presenter.OpenTabNow(MainNavigationTabId.Investment);
            return true;
        }

        public void CloseForSessionReset()
        {
            if (_canonicalPanel != null && _canonicalPanel.IsOpen) _canonicalPanel.CloseNow();
        }

        public string ReadOnlyPortfolioSummaryKo()
        {
            var state = CanonicalGameState;
            if (state == null) return "현재 회사 상태를 불러오는 중입니다.";
            var brokerage = state.StockMarket?.Brokerage;
            var brokerageCash = brokerage?.CashWon ?? 0L;
            var positionCount = brokerage?.Positions.Count ?? 0;
            return $"회사 현금 {state.Company.CashWon:N0}원 · 증권 예수금 {brokerageCash:N0}원 · 보유 종목 {positionCount:N0}개";
        }
    }
}
