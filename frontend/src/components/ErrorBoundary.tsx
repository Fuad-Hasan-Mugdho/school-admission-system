import { Component, type ErrorInfo, type ReactNode } from 'react';

interface State {
  error: Error | null;
}

/** Last line of defence: a rendering bug must never leave the parent staring at a blank page. */
export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('UI crashed', error, info);
  }

  render() {
    if (this.state.error) {
      return (
        <main className="container">
          <div className="alert alert-error" role="alert">
            <strong>দুঃখিত, page-টি দেখাতে গিয়ে একটা সমস্যা হয়েছে।</strong>
            <p>Page reload করলে ঠিক হয়ে যাওয়ার কথা। সমস্যা থাকলে school office-এ জানান।</p>
            <button type="button" className="btn" onClick={() => window.location.reload()}>
              Reload করুন
            </button>
          </div>
        </main>
      );
    }
    return this.props.children;
  }
}
