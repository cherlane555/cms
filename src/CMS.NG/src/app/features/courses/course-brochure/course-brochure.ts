import {
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CourseService } from '@core/services/course.service';
import { Course } from '@core/models/course.model';
import { courseQrSvg } from '../course-qr/course-qr-url';
import { OutlineChapter, parseOutline, stripToText } from './outline-parser';

/**
 * 課程簡章 — a one-page A4 brochure for a single course.
 *
 * Deliberately standalone: no app shell, no nav, no PrimeNG. PrimeNG's theme CSS is a screen
 * design system and has no business inside a print document.
 *
 * See the approved design doc at ~/.gstack/projects/cherlane555-cms/ for why this is a
 * separate component rather than @media print over course-detail, and for the Overflow
 * Contract the fit loop below implements.
 */
@Component({
  selector: 'app-course-brochure',
  imports: [DecimalPipe],
  templateUrl: './course-brochure.html',
  styleUrl: './course-brochure.scss',
  host: { class: 'brochure-host' },
})
export class CourseBrochure implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CourseService);
  private readonly sanitizer = inject(DomSanitizer);

  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
  private readonly sheet = viewChild<ElementRef<HTMLElement>>('sheet');

  protected readonly course = signal<Course | null>(null);
  protected readonly qrSvg = signal<SafeHtml | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  /** Chapters parsed from `Outline`, before the fit loop drops any. */
  private readonly allChapters = signal<OutlineChapter[]>([]);
  /** How many chapters currently render. Reduced by the fit loop until the sheet fits. */
  protected readonly shownCount = signal(Number.MAX_SAFE_INTEGER);

  protected readonly chapters = computed(() => this.allChapters().slice(0, this.shownCount()));
  /** Drives the 節錄 notice next to the QR. Never truncate silently. */
  protected readonly truncated = computed(() => this.shownCount() < this.allChapters().length);

  protected readonly objectiveText = computed(() => {
    const raw = this.course()?.objective;
    return raw ? stripToText(raw).trim() : '';
  });
  protected readonly targetText = computed(() => {
    const raw = this.course()?.target;
    return raw ? stripToText(raw).trim() : '';
  });

  /** Title steps down at measured widths, not character counts — 40 Latin and 40 Han */
  /** characters differ ~2x in width, so a char trigger is the wrong instrument. */
  protected readonly titleClass = computed(() => {
    const len = this.course()?.title?.length ?? 0;
    return len > 44 ? 'title-sm' : len > 26 ? 'title-md' : 'title-lg';
  });

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.getById(id).subscribe({
      next: (course) => {
        this.course.set(course);
        this.allChapters.set(parseOutline(course.outline));
        this.loading.set(false);
        courseQrSvg(course.pkid, course.courseId).then((svg) =>
          this.qrSvg.set(this.sanitizer.bypassSecurityTrustHtml(svg)),
        );
        void this.fit();
      },
      error: () => {
        this.loading.set(false);
        this.error.set('找不到課程');
      },
    });
  }

  /**
   * The Overflow Contract's measure loop. Drops trailing `N.` chapters until the body fits
   * the sheet, then flags the 節錄 notice.
   *
   * This adjusts CONTENT (a bounded walk over a discrete chapter list). Auto-fit — searching
   * the type scale per record — is deliberately not built: that changes the design per course,
   * which is what a designer would hate.
   */
  private async fit(): Promise<void> {
    // Measure against real metrics, not fallback ones. unicode-range faces load lazily as
    // glyphs appear, so fonts.ready alone can resolve before the Han slices are requested.
    await document.fonts.load('9pt "Noto Sans TC"', this.course()?.outline?.slice(0, 200) ?? '中');
    await document.fonts.load('bold 20pt "Noto Sans TC"', this.course()?.title ?? '中');
    await document.fonts.ready;

    const total = this.allChapters().length;
    for (let shown = total; shown >= 0; shown--) {
      this.shownCount.set(shown);
      await new Promise((r) => requestAnimationFrame(r));
      if (!this.overflows() || shown === 0) {
        return;
      }
    }
  }

  /**
   * Measures the content wrapper against the sheet's live area.
   * Deliberately NOT `sheet.scrollHeight > sheet.clientHeight`: the sheet is `overflow: hidden`
   * for clipping, and a non-scrolling box reports its padding-box height for scrollHeight,
   * so that comparison can never fire.
   */
  private overflows(): boolean {
    const body = this.body()?.nativeElement;
    const sheet = this.sheet()?.nativeElement;
    if (!body || !sheet) {
      return false;
    }
    return body.getBoundingClientRect().bottom > sheet.getBoundingClientRect().bottom - 1;
  }

  protected print(): void {
    window.print();
  }
}
