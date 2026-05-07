import { Show } from 'solid-js';
import { useRoot } from './root';
import { evaluate } from './utils';

interface Props {
  item: Action
  index: number
  click: () => any
}

export function SearchItem(props: Props) {

  const { activeIndex, setActiveIndex } = useRoot();

  return (
    <div class="pengu-cmdbar-item-wrap">
      <div
        data-index={props.index}
        data-active={activeIndex() === props.index}
        onClick={props.click}
        onMouseMove={() => setActiveIndex(props.index)}
        class="pengu-cmdbar-item"
      >
        <Show when={props.item.icon}>
          <span innerHTML={props.item.icon}></span>
        </Show>
        <span>{evaluate(props.item.name)}</span>
        <Show when={props.item.legend}>
          <span class="pengu-cmdbar-item-legend">{evaluate(props.item.legend)}</span>
        </Show>
      </div>
    </div>
  )
}