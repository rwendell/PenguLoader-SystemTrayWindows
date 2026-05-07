import { onMount } from 'solid-js';
import { useRoot } from './root';
import { _t } from '../../lib/i18n';

export function SearchBar() {
  let input: HTMLInputElement;
  const { search, setSearch, setActiveIndex } = useRoot();

  onMount(() => {
    setSearch('');
    setActiveIndex(0);
    input.focus();
  });

  const onKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'ArrowUp') {
      e.preventDefault();
    }
  };

  return (
    <div class="pengu-cmdbar-search">
      <svg
        class="pengu-cmdbar-search-icon"
        xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"
        fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
      >
        <circle cx="11" cy="11" r="8"></circle>
        <line x1="21" x2="16.65" y1="21" y2="16.65"></line>
      </svg>
      <input
        ref={input!}
        type="text" value={search()}
        onKeyDown={onKeyDown}
        onInput={e => setSearch(e.target.value)}
        onBlur={() => setTimeout(() => input.focus(), 50)}
        class="pengu-cmdbar-search-input"
        placeholder={_t('cmdbar_type_hint')} autocomplete="off" autocorrect="off" spellcheck={false}
      />
    </div>
  )
}