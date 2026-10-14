import {
  Component,
  computed,
  ElementRef,
  inject,
  model,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, map, of, Subject, switchMap } from 'rxjs';
import { SearchApi } from '../../core/api/search-api';
import { SearchSuggestion, SuggestionType } from '../../core/api/search.model';
import { MAX_SEARCH_LENGTH, validateSearchText } from './search-text';

/** Suggestions are requested only after typing pauses for this long (UX §19). */
export const SUGGESTION_DELAY_MS = 250;
const MIN_SUGGESTION_LENGTH = 2;

const TYPE_LABELS: Record<SuggestionType, string> = {
  plant: 'Plant',
  cultivar: 'Cultivar',
  scientificName: 'Scientific name',
  commonName: 'Common name',
  synonym: 'Synonym',
  plantGroup: 'Plant group',
};

/**
 * Search input with database suggestions (UX §18–19), following the ARIA combobox pattern:
 * ↑/↓ move through suggestions, Enter chooses one (or searches the typed text), Esc closes the list.
 */
@Component({
  selector: 'app-search-box',
  templateUrl: './search-box.html',
  styleUrl: './search-box.css',
})
export class SearchBox {
  private readonly searchApi = inject(SearchApi);
  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('input');
  private readonly typed$ = new Subject<string>();

  readonly text = model('');
  /** Emits trimmed, valid search text when the user searches. */
  readonly submitted = output<string>();

  protected readonly maxLength = MAX_SEARCH_LENGTH;
  protected readonly typeLabels = TYPE_LABELS;
  protected readonly open = signal(false);
  protected readonly activeIndex = signal(-1);
  protected readonly hint = signal<string | null>(null);

  // Only the latest typed text matters: switchMap cancels outdated requests. Failures (including
  // rate limiting) just mean no suggestions; searching still works.
  protected readonly suggestions = toSignal(
    this.typed$.pipe(
      debounceTime(SUGGESTION_DELAY_MS),
      map((text) => text.trim()),
      distinctUntilChanged(),
      switchMap((text) =>
        text.length >= MIN_SUGGESTION_LENGTH && !validateSearchText(text)
          ? this.searchApi.suggest(text).pipe(catchError(() => of<readonly SearchSuggestion[]>([])))
          : of<readonly SearchSuggestion[]>([]),
      ),
    ),
    { initialValue: [] as readonly SearchSuggestion[] },
  );

  protected readonly showSuggestions = computed(() => this.open() && this.suggestions().length > 0);

  focus(): void {
    this.input().nativeElement.focus();
  }

  protected onInput(value: string): void {
    this.text.set(value);
    this.hint.set(null);
    this.open.set(true);
    this.activeIndex.set(-1);
    this.typed$.next(value);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.suggestions().length;
    switch (event.key) {
      case 'ArrowDown':
        if (count > 0) {
          event.preventDefault();
          this.open.set(true);
          this.activeIndex.update((i) => (i + 1) % count);
        }
        break;
      case 'ArrowUp':
        if (count > 0) {
          event.preventDefault();
          this.open.set(true);
          this.activeIndex.update((i) => (i <= 0 ? count - 1 : i - 1));
        }
        break;
      case 'Escape':
        if (this.showSuggestions()) {
          event.preventDefault();
          this.close();
        }
        break;
    }
  }

  /** Enter: the highlighted suggestion if there is one, otherwise the typed text. */
  protected onSubmit(event: Event): void {
    event.preventDefault();
    const index = this.activeIndex();
    if (this.showSuggestions() && index >= 0) {
      this.choose(this.suggestions()[index].text);
    } else {
      this.search(this.text());
    }
  }

  protected choose(text: string): void {
    this.text.set(text);
    this.search(text);
  }

  protected clear(): void {
    this.text.set('');
    this.hint.set(null);
    this.close();
    this.typed$.next('');
    this.focus();
  }

  protected close(): void {
    this.open.set(false);
    this.activeIndex.set(-1);
  }

  private search(text: string): void {
    const value = text.trim();
    const problem = validateSearchText(value);
    if (problem) {
      this.hint.set(problem);
      return;
    }

    this.hint.set(null);
    this.close();
    // Drop any pending suggestion request for text that has now been searched.
    this.typed$.next('');
    this.submitted.emit(value);
  }
}
