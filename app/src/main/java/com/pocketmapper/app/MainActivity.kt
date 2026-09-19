package com.pocketmapper.app

import android.app.Presentation
import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.RectF
import android.hardware.display.DisplayManager
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.Display
import android.view.View
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color as ComposeColor
import androidx.compose.ui.unit.dp
import java.util.Locale

data class ExternalDisplayInfo(
    val display: Display,
    val width: Int,
    val height: Int,
    val refreshRate: Float
)

class MainActivity : ComponentActivity(), DisplayManager.DisplayListener {
    private lateinit var displayManager: DisplayManager
    private val mainHandler = Handler(Looper.getMainLooper())
    private var externalDisplay by mutableStateOf<ExternalDisplayInfo?>(null)
    private var outputRunning by mutableStateOf(false)
    private var presentation: ExternalPresentation? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        displayManager = getSystemService(Context.DISPLAY_SERVICE) as DisplayManager
        refreshExternalDisplay()

        setContent {
            MaterialTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = ComposeColor(0xFF111111),
                    contentColor = ComposeColor.White
                ) {
                    val info = externalDisplay
                    Column(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(24.dp),
                        verticalArrangement = Arrangement.Center
                    ) {
                        Text("PocketMapper", style = MaterialTheme.typography.headlineLarge)
                        Spacer(Modifier.height(24.dp))
                        Text(
                            if (info == null) {
                                "External Display: Not Connected"
                            } else {
                                "External Display: Connected"
                            },
                            style = MaterialTheme.typography.titleMedium
                        )
                        if (info != null) {
                            Spacer(Modifier.height(8.dp))
                            Text("Resolution: ${info.width} × ${info.height}")
                            Text(
                                "Refresh Rate: ${String.format(Locale.US, "%.2f", info.refreshRate)} Hz"
                            )
                        }
                        Spacer(Modifier.height(32.dp))
                        ActionButton("START OUTPUT", info != null && !outputRunning) {
                            startOutput()
                        }
                        ActionButton("TEST PATTERN", info != null && outputRunning) {
                            presentation?.showTestPattern()
                        }
                        ActionButton("BLACKOUT", info != null && outputRunning) {
                            presentation?.blackout()
                        }
                        ActionButton("STOP OUTPUT", outputRunning) {
                            stopOutput()
                        }
                    }
                }
            }
        }
    }

    @androidx.compose.runtime.Composable
    private fun ActionButton(label: String, enabled: Boolean, action: () -> Unit) {
        Button(
            onClick = action,
            enabled = enabled,
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 4.dp)
        ) {
            Text(label)
        }
    }

    override fun onStart() {
        super.onStart()
        displayManager.registerDisplayListener(this, mainHandler)
        refreshExternalDisplay()
    }

    override fun onStop() {
        displayManager.unregisterDisplayListener(this)
        super.onStop()
    }

    override fun onDestroy() {
        stopOutput()
        super.onDestroy()
    }

    override fun onDisplayAdded(displayId: Int) = refreshExternalDisplay()

    override fun onDisplayChanged(displayId: Int) = refreshExternalDisplay()

    override fun onDisplayRemoved(displayId: Int) {
        if (presentation?.display?.displayId == displayId) {
            stopOutput()
        }
        refreshExternalDisplay()
    }

    private fun refreshExternalDisplay() {
        val display = displayManager
            .getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION)
            .firstOrNull()
        externalDisplay = display?.let {
            val mode = it.mode
            ExternalDisplayInfo(it, mode.physicalWidth, mode.physicalHeight, mode.refreshRate)
        }
    }

    private fun startOutput() {
        val display = externalDisplay?.display ?: return
        stopOutput()
        try {
            presentation = ExternalPresentation(this, display).also { it.show() }
            outputRunning = true
        } catch (_: WindowManager.InvalidDisplayException) {
            presentation = null
            outputRunning = false
            refreshExternalDisplay()
        }
    }

    private fun stopOutput() {
        presentation?.dismiss()
        presentation = null
        outputRunning = false
    }
}

private class ExternalPresentation(context: Context, display: Display) :
    Presentation(context, display) {
    private lateinit var patternView: TestPatternView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window?.decorView?.systemUiVisibility =
            View.SYSTEM_UI_FLAG_FULLSCREEN or
                View.SYSTEM_UI_FLAG_HIDE_NAVIGATION or
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
        window?.setBackgroundDrawableResource(android.R.color.black)
        patternView = TestPatternView(context)
        setContentView(patternView)
    }

    fun showTestPattern() {
        patternView.blackout = false
    }

    fun blackout() {
        patternView.blackout = true
    }
}

private class TestPatternView(context: Context) : View(context) {
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        style = Paint.Style.STROKE
        strokeWidth = 3f
    }
    private val textPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textAlign = Paint.Align.CENTER
        typeface = android.graphics.Typeface.DEFAULT_BOLD
    }

    var blackout: Boolean = true
        set(value) {
            field = value
            invalidate()
        }

    override fun onDraw(canvas: Canvas) {
        canvas.drawColor(Color.BLACK)
        if (blackout) return

        val margin = minOf(width, height) * 0.04f
        val left = margin
        val top = margin
        val right = width - margin
        val bottom = height - margin
        val box = RectF(left, top, right, bottom)
        canvas.drawRect(box, paint)

        for (index in 1..3) {
            val x = left + box.width() * index / 4f
            val y = top + box.height() * index / 4f
            canvas.drawLine(x, top, x, bottom, paint)
            canvas.drawLine(left, y, right, y, paint)
        }

        val corner = minOf(box.width(), box.height()) * 0.08f
        drawCorner(canvas, left, top, corner, 1f, 1f)
        drawCorner(canvas, right, top, corner, -1f, 1f)
        drawCorner(canvas, left, bottom, corner, 1f, -1f)
        drawCorner(canvas, right, bottom, corner, -1f, -1f)

        val centerX = width / 2f
        val centerY = height / 2f
        val marker = minOf(width, height) * 0.035f
        canvas.drawCircle(centerX, centerY, marker, paint)
        canvas.drawLine(centerX - marker * 1.5f, centerY, centerX + marker * 1.5f, centerY, paint)
        canvas.drawLine(centerX, centerY - marker * 1.5f, centerX, centerY + marker * 1.5f, paint)

        textPaint.textSize = minOf(width, height) * 0.055f
        canvas.drawText(
            "POCKETMAPPER TEST",
            centerX,
            centerY + box.height() * 0.20f,
            textPaint
        )
    }

    private fun drawCorner(
        canvas: Canvas,
        x: Float,
        y: Float,
        size: Float,
        directionX: Float,
        directionY: Float
    ) {
        canvas.drawLine(x, y, x + size * directionX, y, paint)
        canvas.drawLine(x, y, x, y + size * directionY, paint)
    }
}
