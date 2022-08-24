import {
    CloseScrollStrategy,
    NoopScrollStrategy,
    RepositionScrollStrategy,
    RepositionScrollStrategyConfig,
    ScrollDispatcher,
    ViewportRuler
} from '@angular/cdk/overlay';
import { CloseScrollStrategyConfig } from '@angular/cdk/overlay/scroll/close-scroll-strategy';
import { Inject, Injectable, NgZone } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class NonBlockScrollStrategyOptions {
    constructor(
        private _scrollDispatcher: ScrollDispatcher,
        private _viewportRuler: ViewportRuler,
        private _ngZone: NgZone) { }

    /** Do nothing on scroll. */
    noop = () => new NoopScrollStrategy();
    close = (config?: CloseScrollStrategyConfig) => new CloseScrollStrategy(this._scrollDispatcher,
        this._ngZone, this._viewportRuler, config)
    block = () => new NoopScrollStrategy();
    reposition = (config?: RepositionScrollStrategyConfig) => new RepositionScrollStrategy(
        this._scrollDispatcher, this._viewportRuler, this._ngZone, config)
}
