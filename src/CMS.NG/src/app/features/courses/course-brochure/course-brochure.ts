import {
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { DomSanitizer, SafeHtml, Title } from '@angular/platform-browser';
import { CourseService } from '@core/services/course.service';
import { Course } from '@core/models/course.model';
import { courseQrSvg, publicCourseUrl } from '../course-qr/course-qr-url';
import { OutlineChapter, parseOutline, stripToText } from './outline-parser';

/**
 * The fit loop never drops below this many chapters. Below it the outline stops being a
 * syllabus and becomes a teaser that misrepresents the course, and the reader has no way to
 * tell. Past the floor we clip and say so instead — an announced clip beats a silent lie.
 */
const CHAPTER_FLOOR = 3;

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
export class CourseBrochure implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CourseService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly title = inject(Title);

  /** Restored on leave: the brochure's title is for the PDF, not for the rest of the app. */
  private originalTitle = '';

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

  /**
   * Set from the fit loop's FINAL measurement: the body still exceeds the sheet and is being
   * clipped by `overflow: hidden`. This is a separate fact from dropping chapters — a record
   * with a null `Outline` under a 1,680-char `Objective` (3 exist) has no chapters to drop, so
   * a chapter count can never notice that content vanished.
   */
  private readonly clipped = signal(false);

  protected readonly chapters = computed(() => this.allChapters().slice(0, this.shownCount()));

  /**
   * Drives the 節錄 notice. Content goes missing two ways and BOTH must announce it: the fit
   * loop dropped chapters, or the sheet is still clipping what's left. Counting chapters alone
   * reports `0 < 0` = false on exactly the record that needs the notice most.
   *
   * This is the Overflow Contract's only hard promise: visible, or announced as absent.
   */
  protected readonly truncated = computed(
    () => this.shownCount() < this.allChapters().length || this.clipped(),
  );

  /** The QR is the convenience; the URL is the contract. Printed even when the QR fails. */
  protected readonly publicUrl = computed(() => {
    const c = this.course();
    return c ? publicCourseUrl(c.pkid, c.courseId) : '';
  });

  /**
   * Never promise a QR that isn't on the page. The whole moral defense of truncation is that
   * the reader can still reach the full course — pointing at an absent QR withdraws it.
   */
  protected readonly notice = computed(() => {
    const where = this.qrSvg() ? '請掃描 QR 碼' : '請見下方網址';
    return this.truncated() ? `大綱已節錄，完整內容${where}` : `課程詳細資訊${where}`;
  });

  protected readonly objectiveText = computed(() => {
    const raw = this.course()?.objective;
    return raw ? stripToText(raw).trim() : '';
  });
  protected readonly targetText = computed(() => {
    const raw = this.course()?.target;
    return raw ? stripToText(raw).trim() : '';
  });

  /**
   * Steps down on CHARACTER COUNT — an accepted proxy, not a measurement. 40 Latin and 40 Han
   * characters differ ~2x in width, so the thresholds are tuned for the CJK-dominant case and a
   * long all-Latin title steps down later than it should. Measuring the rendered width would be
   * the right instrument; this is not that, and the longest real title (78 chars) still fits.
   */
  protected readonly titleClass = computed(() => {
    const len = this.course()?.title?.length ?? 0;
    return len > 44 ? 'title-sm' : len > 26 ? 'title-md' : 'title-lg';
  });

  /**
   * Chrome takes the Save-as-PDF filename straight from `document.title` — and stamps that same
   * title on the sheet when the reader leaves headers on. It is the ONLY lever over the filename
   * that exists on the print path, and the app never touches it, so today every brochure saves
   * as `CMSNG.pdf` and the reader has to open it to find out which course it is.
   *
   * CourseId is free-text `varchar(50)` carrying known junk (`'string'`, `'00'`, values with
   * slashes), so it is reduced to filename-safe characters rather than trusted.
   */
  private brochureTitle(course: Course): string {
    const code = course.courseId.replace(/[^A-Za-z0-9._-]/g, '');
    return code ? `${code}-課程簡介` : `課程簡介-${course.pkid}`;
  }

  ngOnDestroy(): void {
    this.title.setTitle(this.originalTitle);
  }

  ngOnInit(): void {
    this.originalTitle = this.title.getTitle();
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.getById(id).subscribe({
      next: (course) => {
        this.course.set(course);
        this.title.setTitle(this.brochureTitle(course));
        this.allChapters.set(parseOutline(course.outline));
        // A failed QR must not leave the footer promising one. `notice` reads qrSvg(), so
        // nulling it here rewrites the sentence rather than stranding the reader.
        courseQrSvg(course.pkid, course.courseId)
          .then((svg) => this.qrSvg.set(this.sanitizer.bypassSecurityTrustHtml(svg)))
          .catch(() => this.qrSvg.set(null));
        void this.settle();
      },
      error: () => {
        this.loading.set(false);
        this.error.set('找不到課程');
      },
    });
  }

  /**
   * `loading` is held until the fit loop has settled, so the reader never watches the sheet
   * reflow and delete its own chapters. The sheet is in the DOM the whole time — it must be, or
   * there is nothing to measure — but `.measuring` hides it until the layout is final.
   *
   * try/finally: a throw in `fit()` must still reveal the sheet. An unfitted brochure is a bad
   * brochure; a permanent 載入中… is no brochure at all.
   */
  private async settle(): Promise<void> {
    try {
      await this.fit();
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * The Overflow Contract's measure loop. 課程目標 is clamped by CSS before this runs, so the
   * prose has already given up its excess; this drops trailing `N.` chapters until the body
   * fits, down to CHAPTER_FLOOR. Whatever is still over the line at the end sets `clipped`.
   *
   * Order matters: prose degrades gracefully (a clamped paragraph still reads as a paragraph),
   * a list degrades categorically — a chapter that isn't there cannot announce its own loss.
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
    const floor = Math.min(CHAPTER_FLOOR, total);
    for (let shown = total; shown >= floor; shown--) {
      this.shownCount.set(shown);
      await new Promise((r) => requestAnimationFrame(r));
      if (!this.overflows()) {
        this.clipped.set(false);
        return;
      }
    }
    // Out of chapters to drop and still over the line: `overflow: hidden` is now cutting real
    // content off the sheet. Say so. This is the branch the old `shownCount < length` test
    // could not see, because with no chapters at all it compared 0 < 0.
    this.clipped.set(true);
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
