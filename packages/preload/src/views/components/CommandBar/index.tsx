import { Show, onMount } from 'solid-js';
import { useRoot, VisualState } from './root';
import { SearchBar } from './SearchBar';
import { SearchResults } from './SearchResults';
import { Animator } from './Animator';
import './style.scss';

export function CommandBar() {

  const { hidden, setVisualState } = useRoot();

  onMount(() => {
    window.addEventListener('keydown', e => {
      if (e.ctrlKey && e.code === 'KeyK' && hidden()) {
        e.preventDefault();
        setVisualState(VisualState.AnimatingIn);
      }
    });
  });

  const backdropClick = () => {
    setVisualState(VisualState.AnimatingOut);
  };

  return (
    <Show when={!hidden()}>
      <div class="pengu-cmdbar-overlay">
        <div class="pengu-cmdbar-backdrop" onClick={backdropClick} />
        <div class="pengu-cmdbar-container">
          <Animator>
            <div class="pengu-cmdbar-panel">
              <SearchBar />
              <SearchResults />
            </div>
          </Animator>
        </div>
      </div>
    </Show>
  )
}