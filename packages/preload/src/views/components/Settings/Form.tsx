import { For, Match, Show, Switch } from 'solid-js';
import type { Field } from '@pengujs/types';

interface EntryLike {
  id: string;
  schema: Record<string, Field>;
  values: () => Record<string, unknown>;
  setValues: (patch: Record<string, unknown>) => void;
}

export function Form(props: { entry: EntryLike }) {
  const fieldList = () => Object.entries(props.entry.schema);
  return (
    <div class="pengu-settings-form">
      <For each={fieldList()}>
        {([key, field]) => <FieldRow id={key} field={field} entry={props.entry} />}
      </For>
    </div>
  );
}

function FieldRow(props: { id: string; field: Field; entry: EntryLike }) {
  const value = () => props.entry.values()[props.id];
  const setValue = (v: unknown) => props.entry.setValues({ [props.id]: v });

  return (
    <Switch>
      <Match when={props.field.type === 'note'}>
        <NoteRow field={props.field as Extract<Field, { type: 'note' }>} />
      </Match>
      <Match when={props.field.type === 'action'}>
        <ActionRow field={props.field as Extract<Field, { type: 'action' }>} />
      </Match>
      <Match when={props.field.type === 'boolean'}>
        <BooleanRow
          field={props.field as Extract<Field, { type: 'boolean' }>}
          value={value() as boolean}
          onChange={setValue}
        />
      </Match>
      <Match when={props.field.type === 'string'}>
        <StringRow
          field={props.field as Extract<Field, { type: 'string' }>}
          value={value() as string}
          onChange={setValue}
        />
      </Match>
      <Match when={props.field.type === 'number'}>
        <NumberRow
          field={props.field as Extract<Field, { type: 'number' }>}
          value={value() as number}
          onChange={setValue}
        />
      </Match>
      <Match when={props.field.type === 'select'}>
        <SelectRow
          field={props.field as Extract<Field, { type: 'select' }>}
          value={value() as string}
          onChange={setValue}
        />
      </Match>
    </Switch>
  );
}

function RowMeta(props: { label: string; description?: string }) {
  return (
    <div class="pengu-settings-row-meta">
      <div class="pengu-settings-row-label">{props.label}</div>
      <Show when={props.description}>
        <div class="pengu-settings-row-description">{props.description}</div>
      </Show>
    </div>
  );
}

function NoteRow(props: { field: Extract<Field, { type: 'note' }> }) {
  return <div class="pengu-settings-note">{props.field.text}</div>;
}

function ActionRow(props: { field: Extract<Field, { type: 'action' }> }) {
  return (
    <div class="pengu-settings-row">
      <RowMeta label={props.field.label} description={props.field.description} />
      <button
        type="button"
        class="pengu-settings-action-btn"
        onClick={() => props.field.perform()}
      >
        {props.field.label}
      </button>
    </div>
  );
}

function BooleanRow(props: {
  field: Extract<Field, { type: 'boolean' }>;
  value: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <label class="pengu-settings-row pengu-settings-row-clickable">
      <RowMeta label={props.field.label} description={props.field.description} />
      <span class={`pengu-settings-toggle${props.value ? ' is-on' : ''}`}>
        <input
          type="checkbox"
          checked={props.value}
          onChange={(e) => props.onChange(e.currentTarget.checked)}
        />
        <span class="pengu-settings-toggle-track">
          <span class="pengu-settings-toggle-thumb" />
        </span>
      </span>
    </label>
  );
}

function StringRow(props: {
  field: Extract<Field, { type: 'string' }>;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div class="pengu-settings-row">
      <RowMeta label={props.field.label} description={props.field.description} />
      <Show
        when={props.field.multiline}
        fallback={
          <input
            type="text"
            class="pengu-settings-input"
            placeholder={props.field.placeholder}
            value={props.value ?? ''}
            onInput={(e) => props.onChange(e.currentTarget.value)}
          />
        }
      >
        <textarea
          class="pengu-settings-input pengu-settings-input-multiline"
          placeholder={props.field.placeholder}
          rows={3}
          onInput={(e) => props.onChange(e.currentTarget.value)}
        >{props.value ?? ''}</textarea>
      </Show>
    </div>
  );
}

function NumberRow(props: {
  field: Extract<Field, { type: 'number' }>;
  value: number;
  onChange: (v: number) => void;
}) {
  const handle = (e: { currentTarget: HTMLInputElement }) => {
    const n = Number(e.currentTarget.value);
    if (!Number.isNaN(n)) props.onChange(n);
  };
  return (
    <div class="pengu-settings-row">
      <RowMeta label={props.field.label} description={props.field.description} />
      <div class="pengu-settings-number-wrap">
        <Show when={props.field.slider}>
          <input
            type="range"
            class="pengu-settings-slider"
            min={props.field.min}
            max={props.field.max}
            step={props.field.step}
            value={props.value}
            onInput={handle}
          />
        </Show>
        <input
          type="number"
          class="pengu-settings-input pengu-settings-input-number"
          min={props.field.min}
          max={props.field.max}
          step={props.field.step}
          value={props.value}
          onInput={handle}
        />
      </div>
    </div>
  );
}

function SelectRow(props: {
  field: Extract<Field, { type: 'select' }>;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div class="pengu-settings-row">
      <RowMeta label={props.field.label} description={props.field.description} />
      <select
        class="pengu-settings-select"
        value={props.value ?? ''}
        onChange={(e) => props.onChange(e.currentTarget.value)}
      >
        <For each={props.field.options}>
          {(opt) => <option value={opt.value}>{opt.label}</option>}
        </For>
      </select>
    </div>
  );
}
