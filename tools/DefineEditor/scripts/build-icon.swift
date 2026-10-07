#!/usr/bin/env swift
// Renders the Polhem.DefineEditor icons from the Polhem organization mark, with the SVG renderer built into macOS:
//   * AppIcon.iconset: the square mark on the macOS icon grid (an 824-pixel body centred on a 1024-pixel canvas)
//     with macOS's corner radius, at the ten sizes Apple's iconset requires. Turn it into AppIcon.icns with
//     `iconutil -c icns AppIcon.iconset`.
//   * polhem.ico: the rounded mark, edge to edge, as PNG entries at 16, 32, 48 and 256 pixels; the main window's
//     icon on Windows and Linux.
//
// The input is the folder holding the brand mark's SVG files, polhem-mark-square.svg and polhem-mark-rounded.svg.
// Copies of them are kept in tools/DefineEditor/Assets/brand, so the icons can be rebuilt from this repository.
// They are copies of the Polhem brand assets: when the mark changes there, copy the new files here and rebuild.
//
// Usage, from the repository root:
//   swift tools/DefineEditor/scripts/build-icon.swift tools/DefineEditor/Assets/brand <output_dir>
//   iconutil -c icns <output_dir>/AppIcon.iconset -o tools/DefineEditor/Assets/AppIcon.icns
//   cp <output_dir>/polhem.ico tools/DefineEditor/Assets/polhem.ico

import AppKit
import Foundation

guard CommandLine.arguments.count == 3 else {
    FileHandle.standardError.write("usage: build-icon.swift <brand_svg_dir> <output_dir>\n".data(using: .utf8)!)
    exit(2)
}

let brandDir = URL(fileURLWithPath: CommandLine.arguments[1])
let outputDir = URL(fileURLWithPath: CommandLine.arguments[2])
let iconsetDir = outputDir.appendingPathComponent("AppIcon.iconset")
try FileManager.default.createDirectory(at: iconsetDir, withIntermediateDirectories: true)

func loadImage(_ name: String) -> NSImage {
    guard let image = NSImage(contentsOf: brandDir.appendingPathComponent(name)) else {
        FileHandle.standardError.write("cannot read \(name) in \(brandDir.path)\n".data(using: .utf8)!)
        exit(1)
    }
    return image
}

let squareMark = loadImage("polhem-mark-square.svg")
let roundedMark = loadImage("polhem-mark-rounded.svg")

/// Renders a transparent square canvas of `pixels` and lets `draw` paint into it.
func render(_ pixels: Int, _ draw: (CGFloat) -> Void) -> Data {
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8,
                               samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                               bytesPerRow: 0, bitsPerPixel: 0)!
    rep.size = NSSize(width: pixels, height: pixels)
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    NSGraphicsContext.current?.imageInterpolation = .high
    draw(CGFloat(pixels))
    NSGraphicsContext.restoreGraphicsState()
    return rep.representation(using: .png, properties: [:])!
}

// The macOS icon grid: on a 1024-pixel canvas the body is 824 pixels, inset 100 pixels, with corners of about
// 185 pixels. Apple's template uses a continuous-curvature squircle; a circular-arc rounded rectangle at the same
// radius is indistinguishable at icon sizes.
func macIcon(_ size: CGFloat) {
    let inset = size * 100 / 1024
    let body = NSRect(x: inset, y: inset, width: size - 2 * inset, height: size - 2 * inset)
    let radius = body.width * 185.4 / 824
    NSBezierPath(roundedRect: body, xRadius: radius, yRadius: radius).addClip()
    squareMark.draw(in: body)
}

let iconsetSizes: [(name: String, pixels: Int)] = [
    ("icon_16x16.png", 16), ("icon_16x16@2x.png", 32),
    ("icon_32x32.png", 32), ("icon_32x32@2x.png", 64),
    ("icon_128x128.png", 128), ("icon_128x128@2x.png", 256),
    ("icon_256x256.png", 256), ("icon_256x256@2x.png", 512),
    ("icon_512x512.png", 512), ("icon_512x512@2x.png", 1024),
]
for (name, pixels) in iconsetSizes {
    try render(pixels, macIcon).write(to: iconsetDir.appendingPathComponent(name))
}

// An ICO file whose entries are PNG images, which Windows Vista and later and Avalonia read.
let icoSizes = [16, 32, 48, 256]
let pngs = icoSizes.map { pixels in render(pixels) { size in roundedMark.draw(in: NSRect(x: 0, y: 0, width: size, height: size)) } }
var ico = Data()
func append16(_ value: Int) { ico.append(contentsOf: [UInt8(value & 0xFF), UInt8((value >> 8) & 0xFF)]) }
func append32(_ value: Int) { append16(value & 0xFFFF); append16((value >> 16) & 0xFFFF) }
append16(0); append16(1); append16(icoSizes.count)
var offset = 6 + 16 * icoSizes.count
for (pixels, png) in zip(icoSizes, pngs) {
    let dimension = UInt8(pixels >= 256 ? 0 : pixels)
    ico.append(contentsOf: [dimension, dimension, 0, 0])
    append16(1); append16(32); append32(png.count); append32(offset)
    offset += png.count
}
for png in pngs { ico.append(png) }
try ico.write(to: outputDir.appendingPathComponent("polhem.ico"))

print("wrote \(iconsetDir.path) and \(outputDir.appendingPathComponent("polhem.ico").path)")
